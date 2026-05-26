#nullable enable
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

	private PackedScene _cardScene = null!;
	private FreeCellState? _pendingSnapshot;
	private readonly Stack<FreeCellState> _undoStack = new();
	private Dictionary<(Suit, Rank), Card> _cardLookup = new();
	private List<(Suit suit, Rank rank)>? _dealOrder;
	private System.Threading.CancellationTokenSource? _autoCompleteCts;

	// ── Lifecycle ──────────────────────────────────────────────────────────

	protected override bool ShowUndoButton => true;
	protected override bool IsGameInProgress => _undoStack.Count > 0 && !_gameWon;

	protected override int EntryCost => 100;
	protected override int WinBonus => 100;
	protected override int FoundationReward => 2;

	private void CancelAutoComplete()
	{
		_autoCompleteCts?.Cancel();
		_autoCompleteCts = null;
	}

	protected override void SetupGame()
	{
#if DEBUG
		EngineTests.RunTests();
#endif
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();
		CreateAllCards();

		var saved = SaveManager.LoadGame<FreeCellState>("FreeCell");
		if (saved != null && !saved.IsFinished)
		{
			ApplyState(saved);
		}
		else
		{
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

	protected override void NewGame() { 
		CancelAutoComplete();
		LoadBackground();
		DealCards(); 
	}
	protected override void RestartGame() { CancelAutoComplete(); DealCards(_dealOrder); }
	protected override void UndoMove()
	{
		CancelAutoComplete();
		if (_undoStack.Count > 0)
		{
			ApplyState(_undoStack.Pop());
			SaveManager.SaveGame("FreeCell", CaptureState());
			_menu.SetUndoEnabled(_undoStack.Count > 0);
		}
	}

	protected override bool CanUndo => _undoStack.Count > 0;

	protected override void HandleMouseButtonDoubleClicked(Vector2 globalPos)
	{
		if (_gameWon) return;
		var card = GetCardAt(globalPos);
		if (card != null)
		{
			if (TryMoveToFoundation(card))
			{
				RunSafeAutoMove();
			}
			return;
		}
		RunSafeAutoMove();
	}

	protected override void HandleCardClick(Card card)
	{
		if (_gameWon) return;
		TryMoveToFoundation(card);
	}

	private bool TryMoveToFoundation(Card card)
	{
		var p = card.CurrentPile;
		if (p == null || card != p.TopCard) return false;

		var currentState = CaptureState();
		var model = new CardModel(card.Suit, card.Rank);
		
		for (int i = 0; i < 4; i++)
		{
			if (FreeCellEngine.CanMove(currentState, new[] { model }, PileType.Foundation, i).IsValid)
			{
				_undoStack.Push(currentState.Clone());
				
				p.RemoveTopCard();
				_foundations[i].AddCard(card);
				
				if (_foundations[i].Count == 1)
				{
					_foundations[i].FoundationSuit = card.Suit;
					_foundationLabels[i].Text = SuitSymbol(card.Suit);
				}

				OnCardMovedToFoundation();
				SaveManager.SaveGame("FreeCell", CaptureState());
				_menu.SetUndoEnabled(true);
				if (FreeCellEngine.IsWon(CaptureState())) EnterWinState();
				return true;
			}
		}
		return false;
	}

	private async void RunSafeAutoMove()
	{
		_autoCompleteCts?.Cancel();
		_autoCompleteCts = new();
		var token = _autoCompleteCts.Token;

		bool totalProgress = false;
		bool movedAny;

		do
		{
			movedAny = false;
			var currentState = CaptureState();
			var sourcePiles = _tableau.Concat(_freeCells).Where(p => !p.IsEmpty).ToList();

			foreach (var pile in sourcePiles)
			{
				if (token.IsCancellationRequested) return;

				var card = pile.TopCard!;
				var model = new CardModel(card.Suit, card.Rank);

				if (FreeCellEngine.IsSafeToMoveToFoundation(currentState, model))
				{
					for (int i = 0; i < 4; i++)
					{
						if (FreeCellEngine.CanMove(currentState, new[] { model }, PileType.Foundation, i).IsValid)
						{
							if (!totalProgress)
							{
								_undoStack.Push(currentState.Clone());
								totalProgress = true;
							}

							var removedCard = pile.RemoveTopCard()!;
							_foundations[i].AddCard(removedCard);
							OnCardMovedToFoundation();
							
							if (_foundations[i].Count == 1)
							{
								_foundations[i].FoundationSuit = removedCard.Suit;
								_foundationLabels[i].Text = SuitSymbol(removedCard.Suit);
							}

							movedAny = true;
							break;
						}
					}
				}
				if (movedAny) break;
			}

			if (movedAny)
			{
				await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
				if (token.IsCancellationRequested) return;
			}

		} while (movedAny);

		if (totalProgress && !token.IsCancellationRequested)
		{
			var state = CaptureState();
			SaveManager.SaveGame("FreeCell", state);
			_menu.SetUndoEnabled(_undoStack.Count > 0);
			if (FreeCellEngine.IsWon(state)) EnterWinState();
		}
	}

	// ── Rules ──────────────────────────────────────────────────────────────

	protected override bool ShouldAllowDrag(Card card)
	{
		var pile = card.CurrentPile;
		if (pile == null || pile.PileType == PileType.Foundation) return false;
		int idx = pile.Cards.IndexOf(card);

		if (pile.PileType == PileType.Tableau && idx < pile.Count - 1)
		{
			var movingCards = pile.Cards.Skip(idx).Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			return FreeCellEngine.IsValidSequence(movingCards);
		}
		return card == pile.TopCard;
	}

	protected override bool CanMoveStack(CardPile pile, Card card, int count) => true;

	protected override CardPile? FindDropTarget(Card draggingCard)
	{
		var allPiles = _freeCells.Concat(_foundations).Concat(_tableau).ToList();
		return OverlapUtils.GetMostOverlapping(draggingCard.GetGlobalRect(), allPiles, p => p.GetGlobalRect());
	}

	protected override bool CanDropCards(Card bottomCard, List<Card> draggingCards, CardPile target)
	{
		var movingModels = draggingCards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		int targetIdx = -1;
		if (target.PileType == PileType.Tableau) targetIdx = Array.IndexOf(_tableau, target);
		else if (target.PileType == PileType.FreeCell) targetIdx = Array.IndexOf(_freeCells, target);
		else if (target.PileType == PileType.Foundation) targetIdx = Array.IndexOf(_foundations, target);

		return FreeCellEngine.CanMove(CaptureState(), movingModels, target.PileType, targetIdx).IsValid;
	}

	protected override void ExecuteDrop(CardPile target, List<Card> draggingCards)
	{
		if (_pendingSnapshot != null) _undoStack.Push(_pendingSnapshot);

		var bottomCard = draggingCards[0];
		if (target.PileType == PileType.Foundation && target.IsEmpty)
		{
			target.FoundationSuit = bottomCard.Suit;
			for (int i = 0; i < 4; i++)
				if (_foundations[i] == target)
					_foundationLabels[i].Text = SuitSymbol(bottomCard.Suit);
		}

		foreach (var c in draggingCards) target.AddCard(c);
	}

	protected override void OnBeforeDragStarted() => _pendingSnapshot = CaptureState();

	protected override void OnDragEnded(bool valid, CardPile? target)
	{
		_pendingSnapshot = null;
		if (valid)
		{
			if (target?.PileType == PileType.Foundation) OnCardMovedToFoundation();
			var state = CaptureState();
			SaveManager.SaveGame("FreeCell", state);
			_menu.SetUndoEnabled(_undoStack.Count > 0);
			if (FreeCellEngine.IsWon(state)) EnterWinState();
		}
	}

	protected override IEnumerable<CardPile> GetPilesForInput() => _foundations.Concat(_freeCells).Concat(_tableau);

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
			_tableau[i].Cascade = CascadeDirection.Vertical;
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

	private void DealCards(List<(Suit suit, Rank rank)>? order = null)
	{
		ExitWinState();

		foreach (var pile in _freeCells.Concat(_foundations).Concat(_tableau))
			while (!pile.IsEmpty)
				pile.RemoveTopCard();

		for (int i = 0; i < 4; i++)
		{
			_foundations[i].FoundationSuit = null;
			_foundationLabels[i].Text = "?";
		}

		_undoStack.Clear();

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
		var state = new FreeCellState { 
			IsFinished = _gameWon,
			BackgroundFile = _currentBackgroundFile
		};
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
		foreach (var card in _cardLookup.Values) card.Visible = false;

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

			_foundationLabels[i].Text = snap.FoundationSuits[i] is Suit s
				? SuitSymbol(s) : "?";
		}
		_gameWon = snap.IsFinished;
	}
}
