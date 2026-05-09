using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.FreeCell.Core;
using EryggGames.FreeCell.Tests;

namespace EryggGames.FreeCell;

public partial class FreeCell : Node2D
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
	private GameState     _pendingSnapshot;
	private bool          _gameWon;
	private Button        _undoBtn;
	private CanvasLayer   _winOverlay;

	private readonly Stack<GameState> _undoStack = new();
	private Dictionary<(Suit, Rank), Card> _cardLookup = new();
	private List<(Suit suit, Rank rank)> _dealOrder;

	// ── Lifecycle ──────────────────────────────────────────────────────────

	private float _topInset = 0f;

	public override void _Ready()
	{
#if DEBUG
		EngineTests.RunTests();
#endif
		_cardScene  = GD.Load<PackedScene>("res://scenes/freecell/Card.tscn");
		_topInset   = GetTopSafeInset();
		LoadBackground();
		SetupMenu();
		SetupPiles();
		DealCards();
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

	private static readonly string[] BackgroundFiles = {
		"20120602_102827.jpg",
		"20120602_104656.jpg",
		"20130902_142522.jpg",
		"DSC_0041.JPG",
		"DSC_0048.JPG",
		"DSC_0052.JPG",
		"DSC_0082.JPG",
		"DSC_0085.JPG",
		"DSC_0111.JPG",
		"DSC_0224.JPG",
		"DSC_0235.JPG",
		"DSC_0254.JPG",
		"DSC_0262.JPG",
		"DSC_0269.JPG",
		"IMG_20140630_121541511.jpg",
		"IMG_20140701_130950770.jpg",
		"IMG_20140701_175445311_HDR.jpg",
		"IMG_20140701_175448478.jpg",
		"IMG_20140701_175458681_HDR.jpg",
		"IMG_20140702_171936154.jpg",
		"IMG_20140702_171955724.jpg",
		"IMG_20140702_192113374.jpg",
		"IMG_20140705_222143348_HDR.jpg",
		"IMG_20140705_230251379_HDR.jpg",
		"IMG_20140705_230753919_HDR.jpg",
		"IMG_20140707_172649625.jpg",
		"IMG_20140926_111210169_HDR.jpg",
		"IMG_20150131_142242627.jpg",
		"IMG_20160530_104229380.jpg",
		"IMG_20160530_104323599.jpg",
		"IMG_20160530_105457716.jpg",
		"IMG_20160902_195617338_HDR.jpg",
		"IMG_20161020_131105943.jpg",
		"IMG_20161021_102855551.jpg",
		"IMG_20161021_115042485.jpg",
		"IMG_20161021_135013001.jpg",
		"IMG_20161021_153221801.jpg",
		"IMG_20161021_161455126_HDR.jpg",
		"IMG_20161021_163113813.jpg",
		"IMG_20161021_173830418.jpg",
		"IMG_20161022_122823449.jpg",
		"IMG_20161022_122903263.jpg",
		"IMG_20161022_123109783_HDR.jpg",
		"IMG_20170808_103553883.jpg",
		"IMG_20170808_105247268.jpg",
		"IMG_20170809_122434218.jpg",
		"IMG_20171226_153037534.jpg",
		"IMG_20171226_163158361_HDR.jpg",
		"IMG_20171227_105002299.jpg",
		"IMG_20171227_110719854.jpg",
		"IMG_20171227_130231974.jpg",
		"IMG_20171227_130644408.jpg",
		"IMG_20171227_131033421.jpg",
		"IMG_20171227_140105994.jpg",
		"IMG_20171227_153703380.jpg",
		"IMG_20171227_162429855.jpg",
		"IMG_20171227_170407672.jpg",
		"IMG_20171228_121734185.jpg",
		"IMG_20171228_151713765.jpg",
		"IMG_20171229_114055447.jpg",
		"IMG_20171229_121355800_HDR.jpg",
		"IMG_20171229_130217928.jpg",
		"IMG_20171229_160549556_HDR.jpg",
		"IMG_20171230_101829174.jpg",
		"IMG_20171230_101958728.jpg",
		"IMG_20171230_102952385.jpg",
		"IMG_20171230_123103568.jpg",
		"IMG_20171230_123245460.jpg",
		"IMG_20180301_174636188.jpg",
		"PXL_20211211_141438732.jpg",
		"PXL_20211220_031246168.jpg",
		"PXL_20220730_161656254.PANO.jpg",
		"PXL_20220730_175812158.PANO.jpg",
		"PXL_20220730_211747874.PANO.jpg",
		"PXL_20220730_232724266.PANO.jpg",
		"PXL_20220730_232938985.jpg",
		"PXL_20220730_235024692.PANO.jpg",
		"PXL_20220730_235121284.jpg",
		"PXL_20220731_175006890.jpg",
		"PXL_20220731_180602609.jpg",
		"PXL_20220801_191347917.PANO.jpg",
		"PXL_20220801_200114292.PANO.jpg",
		"PXL_20220801_204246434.jpg",
		"PXL_20220801_234725280.jpg",
		"PXL_20220802_023702716.jpg",
		"PXL_20220802_024449487.jpg",
		"PXL_20220802_024550394.jpg",
		"PXL_20220802_024628898.MP.jpg",
		"PXL_20220802_024718626.jpg",
		"PXL_20220802_163435130.jpg",
		"PXL_20220802_205857498.jpg",
		"PXL_20220804_163023440.PANO.jpg",
		"PXL_20220804_164515434.MP.jpg",
		"PXL_20220804_171119526.PANO.jpg",
		"PXL_20220804_182925939.MP.jpg",
		"PXL_20220804_202347438.PANO.jpg",
		"PXL_20220804_202440570.PANO.jpg",
		"PXL_20220804_211716972.jpg",
		"PXL_20220804_211746204.PANO.jpg",
		"PXL_20220804_212856820.PANO.jpg",
		"PXL_20220804_213845852.jpg",
		"PXL_20220804_214953211.jpg",
		"PXL_20220804_215531904.PANO.jpg",
		"PXL_20220804_215736500.PANO.jpg",
		"PXL_20220804_220436705.jpg",
		"PXL_20220804_225746333.jpg",
		"PXL_20220804_230236848 (1).jpg",
		"PXL_20220804_230236848.jpg",
		"PXL_20220805_182307780.MP.jpg",
		"PXL_20220805_202459702.PANO.jpg",
		"PXL_20220806_201433717.PANO.jpg",
		"PXL_20220806_201457985.jpg",
		"PXL_20220806_211415984.PANO.jpg",
		"PXL_20220806_220833338.jpg",
		"PXL_20220806_232414375.PANO.jpg",
		"PXL_20220808_194107648.PANO.jpg",
		"PXL_20230214_011006531.jpg",
		"PXL_20230325_141421159.jpg",
		"PXL_20230325_152033107.jpg",
		"PXL_20230529_161917149.jpg",
		"PXL_20230529_170200434.jpg",
		"PXL_20230616_143424336.jpg",
		"PXL_20230704_163753729.jpg",
		"PXL_20230722_170712225.jpg",
		"PXL_20230727_003505202.jpg",
		"PXL_20230727_015006085.jpg",
		"PXL_20230727_202735124.jpg",
		"PXL_20230727_205154821.PANO.jpg",
		"PXL_20230727_211748447.jpg",
		"PXL_20230727_211853586.jpg",
		"PXL_20230727_232714769.jpg",
		"PXL_20230729_200609460.jpg",
		"PXL_20230824_021428824.PANO.jpg",
		"PXL_20231111_223231754.jpg",
		"PXL_20240201_004843978.jpg",
		"PXL_20240511_055542296.jpg",
		"PXL_20240620_002039619.jpg",
		"PXL_20240902_183409028.jpg",
		"PXL_20250715_151714256.jpg",
		"PXL_20250716_120532486.jpg",
		"PXL_20250716_121018881.PANO.jpg",
		"PXL_20250718_092738739.jpg",
		"PXL_20250718_093142948.jpg",
		"PXL_20250718_094554549.jpg",
		"PXL_20250718_100608797.PANO.jpg",
		"PXL_20250718_102046525.PANO.jpg",
		"PXL_20250718_121708808.MP.jpg",
		"PXL_20250718_130959806.jpg",
		"PXL_20250718_131045406.jpg",
		"PXL_20250718_131831376.jpg",
		"PXL_20250719_081416619.jpg",
		"PXL_20250719_081515375.jpg",
		"PXL_20250719_105701716.jpg",
		"PXL_20250719_105919487.jpg",
		"PXL_20250719_105932451.jpg",
		"PXL_20250719_110053109.jpg",
		"PXL_20250719_111916999.jpg",
		"PXL_20250720_114912467.jpg",
		"PXL_20250721_140122003.jpg",
		"PXL_20250721_140340756.jpg",
		"PXL_20250721_154623404.PANO.jpg",
		"PXL_20250721_154642021.jpg",
		"PXL_20250721_154656848.jpg",
		"PXL_20250721_154831629.PANO.jpg",
		"PXL_20250721_155144097.jpg",
		"PXL_20250721_162504125.jpg",
		"PXL_20250721_185509568.MP.jpg",
		"PXL_20250721_185518225.jpg",
		"PXL_20250724_131617009.jpg",
		"PXL_20250724_132724544.jpg",
		"PXL_20250724_133259333.jpg",
		"PXL_20250725_084104097.PANO.jpg",
		"PXL_20250725_085425599.jpg",
		"PXL_20250725_092744516.jpg",
		"PXL_20250725_094201212.jpg",
		"PXL_20250725_100113421.MP.jpg",
		"PXL_20250725_100615880.PANO.jpg",
		"PXL_20250725_100734624.PANO.jpg",
		"PXL_20250725_115636667.jpg",
		"PXL_20250726_055839268.jpg",
		"PXL_20250726_105306777.jpg",
		"PXL_20250728_071430738.jpg",
		"PXL_20250728_072931626.jpg",
		"PXL_20250728_075209132.jpg",
		"PXL_20250728_082725168.jpg",
		"PXL_20250728_082750615.jpg",
		"PXL_20250728_082809269.jpg",
		"PXL_20250728_082913030.jpg",
	};

	private void LoadBackground()
	{
		int idx = GD.RandRange(0, BackgroundFiles.Length - 1);
		var path = $"res://assets/backgrounds/{BackgroundFiles[idx]}";
		var texture = ResourceLoader.Load<Texture2D>(path);
		if (texture == null) return;

		var bg = GetNode<Sprite2D>("Background");
		bg.Texture = texture;

		// Scale to cover the full visible area (phone may be taller than 720x1280)
		var vpSize  = GetViewport().GetVisibleRect().Size;
		var texSize = texture.GetSize();
		float scale = Math.Max(vpSize.X / texSize.X, vpSize.Y / texSize.Y);
		bg.Scale    = new Vector2(scale, scale);
		bg.Position = vpSize / 2;
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

		// Buttons sit below the camera cutout
		float btnY = _topInset + 22f;
		bar.AddChild(MakeMenuButton("New",     new Vector2(60,  btnY), NewGame));
		bar.AddChild(MakeMenuButton("Restart", new Vector2(250, btnY), RestartGame));
		_undoBtn = MakeMenuButton("Undo", new Vector2(440, btnY), UndoMove);
		bar.AddChild(_undoBtn);
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

	public override void _UnhandledInput(InputEvent @event)
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
				EndDrag(_dragCards.Count > 0 ? _dragCards[0].GlobalPosition : Vector2.Zero);
				break;
		}
	}

	private void BeginDrag(Vector2 mousePos)
	{
		if (_gameWon || _autoCompleteShown) return;
		if (_dragCards.Count > 0) return; // guard against double-drag

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

		// Snapshot BEFORE removing anything — correct pre-move state for undo
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
		var target     = GetPileAt(dropCenter);

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

		// --- CLEAR DRAG STATE IMMEDIATELY ---
		_dragCards.Clear();
		_dragOriginPile  = null;
		_dragOffsets     = null;
		_pendingSnapshot = null;

		if (valid)
		{
			var stateAfterMove = CaptureState();
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
		// Find the closest eligible pile within the generous drop radius
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

	// Pickup: exact card bounds
	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth  / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

	// Drop: 1.6× wider, 1.8× taller — more forgiving on small screens
	private static bool IsPointInDropZone(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth  * 0.8f &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight * 0.9f;

	private void DealCards(List<(Suit suit, Rank rank)> order = null)
	{
		ExitWinState();
		CancelDrag();

		// Free all existing cards
		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard()?.QueueFree();

		// Reset foundations
		for (int i = 0; i < 4; i++)
		{
			_foundations[i].FoundationSuit = null;
			_foundationLabels[i].Text = "?";
		}

		_cardLookup.Clear();
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
			var card = _cardScene.Instantiate<Card>();
			card.Init(suit, rank);
			_cardLookup[(suit, rank)] = card;
			_tableau[i % 8].AddCard(card);
		}
	}

	private GameState CaptureState()
	{
		var state = new GameState();
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

	private void ApplyState(GameState snap)
	{
		CancelDrag();

		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard();

		for (int i = 0; i < 8; i++)
			foreach (var cm in snap.Tableau[i])
				_tableau[i].AddCard(_cardLookup[(cm.Suit, cm.Rank)]);

		for (int i = 0; i < 4; i++)
		{
			foreach (var cm in snap.FreeCells[i])
				_freeCells[i].AddCard(_cardLookup[(cm.Suit, cm.Rank)]);

			_foundations[i].FoundationSuit = snap.FoundationSuits[i];
			foreach (var cm in snap.Foundations[i])
				_foundations[i].AddCard(_cardLookup[(cm.Suit, cm.Rank)]);

			_foundationLabels[i].Text = snap.FoundationSuits[i].HasValue
				? SuitSymbol(snap.FoundationSuits[i].Value) : "?";
		}
	}

	private void UndoMove()
	{
		if (_undoStack.Count == 0) return;
		ApplyState(_undoStack.Pop());
	}

	private void NewGame()    { LoadBackground(); DealCards(); }
	private void RestartGame() => DealCards(_dealOrder?.ToList());

	// ── Auto-complete ──────────────────────────────────────────────────────

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
		EnterWinState();
	}

	// ── Dialogs ────────────────────────────────────────────────────────────

	private void ShowAutoCompleteDialog()
	{
		_autoCompleteShown = true;

		var vpSize = GetViewport().GetVisibleRect().Size;
		float cx = vpSize.X / 2f;
		float cy = vpSize.Y / 2f;

		var layer = new CanvasLayer { Layer = 20 };
		AddChild(layer);

		layer.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), Size = vpSize });

		var box = new ColorRect
		{
			Color    = new Color(0.12f, 0.15f, 0.18f, 0.97f),
			Position = new Vector2(cx - 210, cy - 100),
			Size     = new Vector2(420, 200),
		};
		layer.AddChild(box);

		var lbl = new Label { Text = "Auto-finish game?", Position = new Vector2(cx - 145, cy - 72) };
		lbl.AddThemeFontSizeOverride("font_size", 28);
		layer.AddChild(lbl);

		layer.AddChild(MakeMenuButton("Yes", new Vector2(cx - 200, cy - 10), () =>
		{
			layer.QueueFree();
			AutoFinish();
		}));
		layer.AddChild(MakeMenuButton("No", new Vector2(cx + 10, cy - 10), () =>
		{
			_autoCompleteShown = false;
			layer.QueueFree();
		}));
	}

	private void EnterWinState()
	{
		_gameWon = true;
		GetNode<Node2D>("FreeCells").Visible   = false;
		GetNode<Node2D>("Foundations").Visible = false;
		GetNode<Node2D>("Tableau").Visible     = false;
		if (_undoBtn != null) _undoBtn.Disabled = true;

		_winOverlay = new CanvasLayer { Layer = 5 };
		AddChild(_winOverlay);

		var vpSize = GetViewport().GetVisibleRect().Size;

		var band = new ColorRect
		{
			Color    = new Color(0f, 0f, 0f, 0.45f),
			Position = new Vector2(0, vpSize.Y / 2f - 50),
			Size     = new Vector2(vpSize.X, 100),
		};
		_winOverlay.AddChild(band);

		var lbl = new Label
		{
			Text                = "You Won!",
			HorizontalAlignment = HorizontalAlignment.Center,
			Size                = new Vector2(vpSize.X, 80),
			Position            = new Vector2(0, vpSize.Y / 2f - 40),
		};
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
