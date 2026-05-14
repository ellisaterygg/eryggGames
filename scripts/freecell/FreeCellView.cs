using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.FreeCell.Core;
using EryggGames.FreeCell.Tests;

namespace EryggGames.FreeCell;

public partial class FreeCellView : BaseGameView
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
	private readonly Stack<FreeCellState> _undoStack = new();
	private Dictionary<(Suit, Rank), Card> _cardLookup = new();
	private List<(Suit suit, Rank rank)> _dealOrder;

	// ── Lifecycle ──────────────────────────────────────────────────────────

	protected override void SetupGame()
	{
#if DEBUG
		EngineTests.RunTests();
#endif
		_cardScene  = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();
		CreateAllCards(); 

		var saved = SaveManager.LoadGame<FreeCellState>("FreeCell");
		if (saved != null && !saved.IsFinished)
		{
			GD.Print("Loaded saved FreeCell game.");
			ApplyState(saved);
		}
		else
		{
			GD.Print("Starting new FreeCell game.");
			DealCards();
		}

		_menu.SetUndoEnabled(_undoStack.Count > 0);
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

	protected override void NewGame() => DealCards();
	protected override void RestartGame() => DealCards(_dealOrder);
	protected override void UndoMove()
	{
		if (_undoStack.Count > 0)
		{
			ApplyState(_undoStack.Pop());
			SaveManager.SaveGame("FreeCell", CaptureState());
			_menu.SetUndoEnabled(_undoStack.Count > 0);
		}
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
			_menu.SetUndoEnabled(_undoStack.Count > 0);
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

	private Card GetCardAt(Vector2 pos)
	{
		var allPiles = _foundations.Concat(_freeCells).Concat(_tableau);
		foreach (var pile in allPiles)
			for (int i = pile.Count - 1; i >= 0; i--)
				if (IsPointInCard(pos, pile.Cards[i].GlobalPosition))
					return pile.Cards[i];
		return null;
	}

	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth  / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

	private void DealCards(List<(Suit suit, Rank rank)> order = null)
	{
		ExitWinState();
		CancelDrag();

		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard();

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
		_menu.SetUndoEnabled(false);
	}

	private FreeCellState CaptureState()
	{
		var state = new FreeCellState { IsFinished = _gameWon };
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
		_gameWon = snap.IsFinished;
	}

	private void ShowAutoCompleteDialog()
	{
		_autoCompleteShown = true;
		var popup = new CanvasLayer { Layer = 10 };
		AddChild(popup);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		popup.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(300, 150) };
		center.AddChild(panel);

		var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		panel.AddChild(vBox);

		vBox.AddChild(new Label { Text = "Auto-complete available!", HorizontalAlignment = HorizontalAlignment.Center });
		
		var btn = new Button { Text = "Finish Game" };
		btn.Pressed += () => {
			popup.QueueFree();
			RunAutoComplete();
		};
		vBox.AddChild(btn);

		var cancel = new Button { Text = "Not now" };
		cancel.Pressed += () => popup.QueueFree();
		vBox.AddChild(cancel);
	}

	private async void RunAutoComplete()
	{
		while (FreeCellEngine.CanAutoComplete(CaptureState()))
		{
			var state = CaptureState();
			var move = FreeCellEngine.GetAutoCompleteMove(state);
			if (move == null) break;

			Card card = null;
			if (move.FromType == PileType.Tableau) card = _tableau[move.FromIdx].RemoveTopCard();
			else card = _freeCells[move.FromIdx].RemoveTopCard();

			_foundations[move.ToIdx].AddCard(card);
			
			if (_foundations[move.ToIdx].Count == 1)
			{
				_foundations[move.ToIdx].FoundationSuit = card.Suit;
				_foundationLabels[move.ToIdx].Text = SuitSymbol(card.Suit);
			}

			await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
			if (FreeCellEngine.IsWon(CaptureState()))
			{
				EnterWinState();
				break;
			}
		}
		SaveManager.SaveGame("FreeCell", CaptureState());
	}
}
