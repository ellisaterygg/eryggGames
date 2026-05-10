using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.FreeCell.Core;
using EryggGames.FreeCell.Tests;

namespace EryggGames.FreeCell;

public partial class FreeCellView : Node2D
{
	private readonly CardPile[] _tableau    = new CardPile[8];
	private readonly CardPile[] _freeCells  = new CardPile[4];
	private readonly CardPile[] _foundations = new CardPile[4];
	private readonly Label[]    _foundationLabels = new Label[4];

	private PackedScene _cardScene;

	// Drag state
	private readonly List<Card> _dragCards = new();
	private CardPile  _dragOriginPile;
	private Vector2[] _dragOffsets;

	private bool          _autoCompleteShown;
	private FreeCellState _pendingSnapshot;
	private bool          _gameWon;
	private Button        _undoBtn;
	private CanvasLayer   _winOverlay;

	private readonly Stack<FreeCellState> _undoStack = new();
	private Dictionary<(Suit, Rank), Card> _cardLookup = new();
	private List<(Suit suit, Rank rank)> _dealOrder;

	// ── Lifecycle ──────────────────────────────────────────────────────────

	private float _topInset = 0f;

	public override void _Ready()
	{
#if DEBUG
		EngineTests.RunTests();
#endif
		_cardScene  = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		_topInset   = GetTopSafeInset();
		LoadBackground();
		SetupMenu();
		SetupPiles();
		CreateAllCards(); 

		var saved = SaveManager.LoadGame<FreeCellState>("FreeCell");
		if (saved != null)
		{
			GD.Print("Loaded saved FreeCell game.");
			ApplyState(saved);
		}
		else
		{
			GD.Print("Starting new FreeCell game.");
			DealCards();
		}
	}

	private void CreateAllCards()
	{
		_cardLookup.Clear();
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				var card = _cardScene.Instantiate<Card>();
				card.Init(suit, rank);
				card.Visible = false;
				AddChild(card);
				_cardLookup[(suit, rank)] = card;
			}
		}
	}

	// Convert screen-pixel top safe-area inset to game units
	private float GetTopSafeInset()
	{
		var screenH  = (float)DisplayServer.ScreenGetSize().Y;
		var safeTopPx = (float)DisplayServer.GetDisplaySafeArea().Position.Y;
		if (safeTopPx <= 0f || screenH <= 0f) return 0f;
		return safeTopPx / screenH * GetViewport().GetVisibleRect().Size.Y;
	}

	// ── Background ─────────────────────────────────────────────────────────

	private void LoadBackground()
	{
		BackgroundManager.LoadRandomBackground(GetNode<Sprite2D>("Background"));
	}

	// ── Menu ───────────────────────────────────────────────────────────────

	private void SetupMenu()
	{
		var layer = new CanvasLayer();
		AddChild(layer);

		float barH = 95f + _topInset;
		var bar = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.50f),
			Size  = new Vector2(720, barH),
		};
		layer.AddChild(bar);

		float btnY = _topInset + 22f;
		bar.AddChild(MakeMenuButton("New",     new Vector2(30,  btnY), NewGame));
		bar.AddChild(MakeMenuButton("Restart", new Vector2(200, btnY), RestartGame));
		_undoBtn = MakeMenuButton("Undo", new Vector2(370, btnY), UndoMove);
		bar.AddChild(_undoBtn);
		bar.AddChild(MakeMenuButton("Games", new Vector2(540, btnY), ShowGameSelection));
	}

	private void ShowGameSelection()
	{
		var launcher = GetParent<Launcher>();
		if (launcher != null)
		{
			launcher.SwitchGame("Launcher");
		}
	}

	private static Button MakeMenuButton(string text, Vector2 pos, Action handler)
	{
		var btn = new Button
		{
			Text     = text,
			Position = pos,
			Size     = new Vector2(130, 52),
		};
		btn.AddThemeFontSizeOverride("font_size", 20);
		btn.Pressed += handler;
		return btn;
	}

	// ── Piles / labels ─────────────────────────────────────────────────────

	private void SetupPiles()
	{
		var safeOffset = new Vector2(0, _topInset);

		for (int i = 0; i < 4; i++)
		{
			_freeCells[i] = GetNode<CardPile>($"FreeCells/Cell{i}");
			_freeCells[i].Position += safeOffset;
			_freeCells[i].PileType = PileType.FreeCell;
			_freeCells[i].AddChild(MakeLabel("FREE", 10, new Vector2(-Card.CardWidth / 2 + 3, -Card.CardHeight / 2 + 2)));
		}

		for (int i = 0; i < 4; i++)
		{
			_foundations[i] = GetNode<CardPile>($"Foundations/Foundation{i}");
			_foundations[i].Position += safeOffset;
			_foundations[i].PileType = PileType.Foundation;
			_foundationLabels[i] = MakeLabel("?", 14, new Vector2(-6, -Card.CardHeight / 2 + 2));
			_foundations[i].AddChild(_foundationLabels[i]);
		}

		for (int i = 0; i < 8; i++)
		{
			_tableau[i] = GetNode<CardPile>($"Tableau/Column{i}");
			_tableau[i].Position += safeOffset;
			_tableau[i].PileType = PileType.Tableau;
		}
	}

	private static Label MakeLabel(string text, int fontSize, Vector2 pos)
	{
		var label = new Label { Text = text, Position = pos, ZIndex = 0 };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private static string SuitSymbol(Suit suit) => suit switch
	{
		Suit.Clubs    => "♣",
		Suit.Diamonds => "♦",
		Suit.Hearts   => "♥",
		Suit.Spades   => "♠",
		_             => "?"
	};

	// ── Input ──────────────────────────────────────────────────────────────

	public override void _Input(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
				GD.Print($"FreeCell Click at {mb.Position}");
				BeginDrag(mb.Position);
				break;
			case InputEventMouseMotion mm when _dragCards.Count > 0:
				UpdateDrag(mm.Position);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
				if (_dragCards.Count > 0)
					EndDrag(_dragCards[0].GlobalPosition);
				break;
		}
	}

	private void BeginDrag(Vector2 mousePos)
	{
		if (_gameWon || _autoCompleteShown) return;
		if (_dragCards.Count > 0) return;

		var card = GetCardAt(mousePos);
		if (card == null) return;

		var pile = card.CurrentPile;
		int idx  = pile.Cards.IndexOf(card);

		if (pile.PileType == PileType.Tableau && idx < pile.Count - 1)
		{
			var movingCards = pile.Cards.Skip(idx).Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			if (!FreeCellEngine.IsValidSequence(movingCards)) return;
		}
		else if (card != pile.TopCard) return;

		_pendingSnapshot = CaptureState();

		int count = pile.Count - idx;
		_dragOriginPile = pile;

		var globalPos = new Vector2[count];
		for (int i = 0; i < count; i++)
			globalPos[i] = pile.Cards[idx + i].GlobalPosition;

		var cards = pile.RemoveTopCards(count);
		_dragOffsets = new Vector2[count];

		for (int i = 0; i < count; i++)
		{
			AddChild(cards[i]);
			cards[i].Position = globalPos[i];
			_dragOffsets[i]   = globalPos[i] - mousePos;
			cards[i].ZIndex   = 100 + i;
			cards[i].Modulate = new Color(0.7f, 1f, 0.7f);
			_dragCards.Add(cards[i]);
		}
	}

	private void UpdateDrag(Vector2 mousePos)
	{
		for (int i = 0; i < _dragCards.Count; i++)
			_dragCards[i].Position = mousePos + _dragOffsets[i];
	}

	private void EndDrag(Vector2 dropCenter)
	{
		if (_dragCards.Count == 0) return;

		var bottomCard = _dragCards[0];
		int count      = _dragCards.Count;
		
		// --- NEW AREA-BASED HIT DETECTION ---
		var allPiles = _freeCells.Concat(_foundations).Concat(_tableau).ToList();
		var target = OverlapUtils.GetMostOverlapping(bottomCard.GetGlobalRect(), allPiles, p => p.GetGlobalRect());

		bool valid = false;
		if (target != null && target != _dragOriginPile)
		{
			var movingModels = _dragCards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			int targetIdx = -1;
			if (target.PileType == PileType.Tableau) targetIdx = Array.IndexOf(_tableau, target);
			else if (target.PileType == PileType.FreeCell) targetIdx = Array.IndexOf(_freeCells, target);
			else if (target.PileType == PileType.Foundation) targetIdx = Array.IndexOf(_foundations, target);

			var state = CaptureState();
			valid = FreeCellEngine.CanMove(state, movingModels, target.PileType, targetIdx).IsValid;
		}

		if (valid)
		{
			if (_pendingSnapshot != null)
				_undoStack.Push(_pendingSnapshot);

			if (target.PileType == PileType.Foundation && target.IsEmpty)
			{
				target.FoundationSuit = bottomCard.Suit;
				for (int i = 0; i < 4; i++)
					if (_foundations[i] == target)
						_foundationLabels[i].Text = SuitSymbol(bottomCard.Suit);
			}
		}

		var destination = valid ? target : _dragOriginPile;

		foreach (var c in _dragCards)
		{
			c.Modulate = Colors.White;
			destination.AddCard(c);
		}

		_dragCards.Clear();
		_dragOriginPile  = null;
		_dragOffsets     = null;
		_pendingSnapshot = null;

		if (valid)
		{
			var stateAfterMove = CaptureState();
			SaveManager.SaveGame("FreeCell", stateAfterMove);
			if (FreeCellEngine.IsWon(stateAfterMove))
				EnterWinState();
			else if (!_autoCompleteShown && FreeCellEngine.CanAutoComplete(stateAfterMove))
				ShowAutoCompleteDialog();
		}
	}

	private void CancelDrag()
	{
		if (_dragCards.Count == 0) return;
		foreach (var c in _dragCards)
		{
			c.Modulate = Colors.White;
			_dragOriginPile.AddCard(c);
		}
		_dragCards.Clear();
		_dragOriginPile = null;
		_dragOffsets    = null;
	}

	// ── Hit testing ────────────────────────────────────────────────────────

	private Card GetCardAt(Vector2 pos)
	{
		var allPiles = _foundations.Concat(_freeCells).Concat(_tableau);
		foreach (var pile in allPiles)
			for (int i = pile.Count - 1; i >= 0; i--)
				if (IsPointInCard(pos, pile.Cards[i].GlobalPosition))
					return pile.Cards[i];
		return null;
	}

	private CardPile GetPileAt(Vector2 pos)
	{
		CardPile best  = null;
		float bestDist = float.MaxValue;

		var allPiles = _freeCells.Concat(_foundations).Concat(_tableau);
		foreach (var pile in allPiles)
		{
			var check = pile.IsEmpty ? pile.GlobalPosition : pile.TopCard.GlobalPosition;
			if (!IsPointInDropZone(pos, check)) continue;
			float dist = pos.DistanceTo(check);
			if (dist < bestDist) { bestDist = dist; best = pile; }
		}
		return best;
	}

	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth  / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

	private static bool IsPointInDropZone(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth  * 0.8f &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight * 0.9f;

	private void DealCards(List<(Suit suit, Rank rank)> order = null)
	{
		ExitWinState();
		CancelDrag();

		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard(); // Don't queue free, we reuse them

		for (int i = 0; i < 4; i++)
		{
			_foundations[i].FoundationSuit = null;
			_foundationLabels[i].Text = "?";
		}

		_undoStack.Clear();
		_autoCompleteShown = false;

		if (order == null)
		{
			var deck = new Deck();
			order = deck.Shuffle();
		}
		_dealOrder = order;

		for (int i = 0; i < order.Count; i++)
		{
			var (suit, rank) = order[i];
			var card = _cardLookup[(suit, rank)];
			card.Visible = true;
			_tableau[i % 8].AddCard(card);
		}
		SaveManager.SaveGame("FreeCell", CaptureState());
	}

	private FreeCellState CaptureState()
	{
		var state = new FreeCellState();
		for (int i = 0; i < 8; i++)
			state.Tableau[i] = _tableau[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		for (int i = 0; i < 4; i++)
		{
			state.FreeCells[i] = _freeCells[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			state.Foundations[i] = _foundations[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			state.FoundationSuits[i] = _foundations[i].FoundationSuit;
		}
		return state;
	}

	private void ApplyState(FreeCellState snap)
	{
		CancelDrag();

		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard();

		for (int i = 0; i < 8; i++)
			foreach (var cm in snap.Tableau[i])
			{
				var card = _cardLookup[(cm.Suit, cm.Rank)];
				card.Visible = true;
				_tableau[i].AddCard(card);
			}

		for (int i = 0; i < 4; i++)
		{
			foreach (var cm in snap.FreeCells[i])
			{
				var card = _cardLookup[(cm.Suit, cm.Rank)];
				card.Visible = true;
				_freeCells[i].AddCard(card);
			}

			_foundations[i].FoundationSuit = snap.FoundationSuits[i];
			foreach (var cm in snap.Foundations[i])
			{
				var card = _cardLookup[(cm.Suit, cm.Rank)];
				card.Visible = true;
				_foundations[i].AddCard(card);
			}

			_foundationLabels[i].Text = snap.FoundationSuits[i].HasValue
				? SuitSymbol(snap.FoundationSuits[i].Value) : "?";
		}
	}

	private void UndoMove()
	{
		if (_undoStack.Count == 0) return;
		var state = _undoStack.Pop();
		ApplyState(state);
		SaveManager.SaveGame("FreeCell", state);
	}

	private void NewGame()    { LoadBackground(); DealCards(); }
	private void RestartGame() => DealCards(_dealOrder?.ToList());

	private void AutoFinish()
	{
		CancelDrag();
		_undoStack.Clear();
		var sourcePiles = _tableau.Concat(_freeCells).ToList();
		bool progress = true;
		while (progress)
		{
			progress = false;
			foreach (var pile in sourcePiles)
			{
				if (pile.IsEmpty) continue;
				var card = pile.TopCard;
				var cardModel = new CardModel(card.Suit, card.Rank);
				for (int i = 0; i < 4; i++)
				{
					var state = CaptureState();
					if (!FreeCellEngine.CanMove(state, new[] { cardModel }, PileType.Foundation, i).IsValid) continue;
					if (_foundations[i].IsEmpty)
					{
						_foundations[i].FoundationSuit = card.Suit;
						_foundationLabels[i].Text = SuitSymbol(card.Suit);
					}
					pile.RemoveTopCard();
					_foundations[i].AddCard(card);
					progress = true;
					break;
				}
				if (progress) break;
			}
		}
		SaveManager.SaveGame("FreeCell", CaptureState());
		EnterWinState();
	}

	private void ShowAutoCompleteDialog()
	{
		_autoCompleteShown = true;
		var vpSize = GetViewport().GetVisibleRect().Size;
		float cx = vpSize.X / 2f;
		float cy = vpSize.Y / 2f;

		var layer = new CanvasLayer { Layer = 20 };
		AddChild(layer);
		layer.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), Size = vpSize });
		var box = new ColorRect { Color = new Color(0.12f, 0.15f, 0.18f, 0.97f), Position = new Vector2(cx - 210, cy - 100), Size = new Vector2(420, 200) };
		layer.AddChild(box);
		var lbl = new Label { Text = "Auto-finish game?", Position = new Vector2(cx - 145, cy - 72) };
		lbl.AddThemeFontSizeOverride("font_size", 28);
		layer.AddChild(lbl);

		layer.AddChild(MakeMenuButton("Yes", new Vector2(cx - 200, cy - 10), () => { layer.QueueFree(); AutoFinish(); }));
		layer.AddChild(MakeMenuButton("No", new Vector2(cx + 10, cy - 10), () => { _autoCompleteShown = false; layer.QueueFree(); }));
	}

	private void EnterWinState()
	{
		_gameWon = true;
		GetNode<Node2D>("FreeCells").Visible   = false;
		GetNode<Node2D>("Foundations").Visible = false;
		GetNode<Node2D>("Tableau").Visible     =     false;
		if (_undoBtn != null) _undoBtn.Disabled = true;
		_winOverlay = new CanvasLayer { Layer = 5 };
		AddChild(_winOverlay);
		var vpSize = GetViewport().GetVisibleRect().Size;
		var band = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), Position = new Vector2(0, vpSize.Y / 2f - 50), Size = new Vector2(vpSize.X, 100) };
		_winOverlay.AddChild(band);
		var lbl = new Label { Text = "You Won!", HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(vpSize.X, 80), Position = new Vector2(0, vpSize.Y / 2f - 40) };
		lbl.AddThemeFontSizeOverride("font_size", 52);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		_winOverlay.AddChild(lbl);
	}

	private void ExitWinState()
	{
		if (!_gameWon) return;
		_gameWon = false;
		GetNode<Node2D>("FreeCells").Visible   = true;
		GetNode<Node2D>("Foundations").Visible = true;
		GetNode<Node2D>("Tableau").Visible     = true;
		if (_undoBtn != null) _undoBtn.Disabled = false;
		_winOverlay?.QueueFree();
		_winOverlay = null;
	}
}
