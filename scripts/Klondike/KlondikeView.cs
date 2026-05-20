#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.Klondike.Core;

namespace EryggGames.Klondike;

public partial class KlondikeView : BaseGameView
{
	private CardPile _stockPile = null!;
	private CardPile _wastePile = null!;
	private readonly CardPile[] _tableau = new CardPile[7];
	private readonly CardPile[] _foundations = new CardPile[4];

	private KlondikeState _state = new();
	private readonly Stack<KlondikeState> _undoStack = new();
	private Dictionary<(Suit, Rank), Card> _cardLookup = new();
	private PackedScene _cardScene = null!;

	private List<GameOption> _options = new();
	private System.Threading.CancellationTokenSource? _autoMoveCts;

	protected override void SetupGame()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		
		var safeOffset = new Vector2(0, _topInset);

		float startX = 75f;
		float pitchX = 95f;

		_stockPile = GetNode<CardPile>("Stock");
		_stockPile.Position = new Vector2(startX, 160) + safeOffset;
		_stockPile.PileType = PileType.FreeCell; 
		
		_wastePile = GetNode<CardPile>("Waste");
		_wastePile.Position = new Vector2(startX + pitchX, 160) + safeOffset;
		_wastePile.PileType = PileType.FreeCell;
		_wastePile.Cascade = CascadeDirection.Horizontal;
		_wastePile.CardOffset = 30f;

		for (int i = 0; i < 4; i++)
		{
			_foundations[i] = GetNode<CardPile>($"Foundations/Foundation{i}");
			_foundations[i].Position = new Vector2(startX + (i + 3) * pitchX, 160) + safeOffset;
			_foundations[i].PileType = PileType.Foundation;
		}

		for (int i = 0; i < 7; i++)
		{
			_tableau[i] = GetNode<CardPile>($"Tableau/Column{i}");
			_tableau[i].Position = new Vector2(startX + i * pitchX, 300) + safeOffset;
			_tableau[i].PileType = PileType.Tableau;
			_tableau[i].Cascade = CascadeDirection.Vertical;
			_tableau[i].CardOffset = 40f; 
		}

		CreateAllCards();

		var saved = SaveManager.LoadGame<KlondikeState>("Klondike");
		if (saved != null && !saved.IsFinished)
		{
			_state = saved;
			ApplyState(_state);
		}
		else
		{
			NewGame();
		}
		SetupOptionsMenu();
	}

	private void SetupOptionsMenu()
	{
		_options = new List<GameOption>
		{
			new GameOption { 
				Id = "draw_count", 
				Label = "Draw Count", 
				Options = new[] { "Draw 1", "Draw 3" }, 
				SelectedIndex = _state.DrawCount == 1 ? 0 : 1 
			}
		};
		_menu.SetOptions(_options);
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

	protected override void NewGame()
	{
		ExitWinState();
		LoadBackground();

		var deck = new Deck().Shuffle();
		_state.InitialDeal = deck.Select(c => new CardModel(c.suit, c.rank)).ToList();
		_undoStack.Clear();
		DealFromOrder(_state.InitialDeal);
	}

	protected override void RestartGame()
	{
		ExitWinState();
		if (_state.InitialDeal.Count == 0) NewGame();
		else
		{
			_undoStack.Clear();
			DealFromOrder(_state.InitialDeal);
		}
	}

	private void DealFromOrder(List<CardModel> order)
	{
		foreach (var p in GetPilesForInput()) while (!p.IsEmpty) p.RemoveTopCard();

		int idx = 0;
		for (int i = 0; i < 7; i++)
		{
			_state.TableauFaceDown[i] = i;
			for (int j = 0; j <= i; j++)
			{
				var model = order[idx++];
				var card = _cardLookup[(model.Suit, model.Rank)];
				card.Visible = true;
				card.IsFaceUp = (j == i);
				_tableau[i].AddCard(card);
			}
		}

		_state.Stock.Clear();
		while (idx < order.Count)
		{
			var model = order[idx++];
			var card = _cardLookup[(model.Suit, model.Rank)];
			card.Visible = true;
			card.IsFaceUp = false;
			_stockPile.AddCard(card);
		}

		_state.Waste.Clear();
		for (int i = 0; i < 4; i++) _state.Foundation[i].Clear();

		UpdateStateFromPiles();
		SaveGame();
	}

	protected override void OnOptionsApplied(bool startNewGame)
	{
		var drawOpt = _options.First(o => o.Id == "draw_count");
		_state.DrawCount = drawOpt.SelectedIndex == 0 ? 1 : 3;

		if (startNewGame) NewGame();
		else SaveGame();
	}

	protected override void UndoMove()
	{
		_autoMoveCts?.Cancel();
		if (_undoStack.Count > 0)
		{
			_state = _undoStack.Pop();
			ApplyState(_state);
			SaveGame();
		}
	}

	protected override void HandleMouseButtonDoubleClicked(Vector2 globalPos)
	{
		if (_gameWon) return;
		if (GetCardAt(globalPos) != null) return;
		RunSafeAutoMove();
	}

	private async void RunSafeAutoMove()
	{
		_autoMoveCts?.Cancel();
		_autoMoveCts = new();
		var token = _autoMoveCts.Token;

		bool totalProgress = false;
		bool movedAny;

		do
		{
			movedAny = false;
			var currentState = CaptureState();

			// Only pull from tableau as requested
			foreach (var pile in _tableau)
			{
				if (pile.IsEmpty) continue;
				if (token.IsCancellationRequested) return;

				var card = pile.TopCard!;
				if (!card.IsFaceUp) continue;

				var model = new CardModel(card.Suit, card.Rank);

				if (KlondikeEngine.IsSafeToMoveToFoundation(currentState, model))
				{
					for (int i = 0; i < 4; i++)
					{
						if (KlondikeEngine.CanMove(currentState, new List<CardModel> { model }, PileType.Foundation, i))
						{
							if (!totalProgress)
							{
								_undoStack.Push(currentState);
								totalProgress = true;
							}

							var removedCard = pile.RemoveTopCard()!;
							_foundations[i].AddCard(removedCard);
							
							movedAny = true;
							break;
						}
					}
				}
				if (movedAny) break;
			}

			if (movedAny)
			{
				CheckTableauFlip();
				UpdateStateFromPiles();
				await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
				if (token.IsCancellationRequested) return;
			}

		} while (movedAny);

		if (totalProgress && !token.IsCancellationRequested)
		{
			UpdateStateFromPiles();
			SaveGame();
			if (KlondikeEngine.IsWon(_state)) EnterWinState();
		}
	}

	private void SaveGame()
	{
		SaveManager.SaveGame("Klondike", _state);
		_menu.SetUndoEnabled(_undoStack.Count > 0);
	}

	private void UpdateStateFromPiles()
	{
		for (int i = 0; i < 7; i++)
		{
			_state.Tableau[i] = _tableau[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			_state.TableauFaceDown[i] = _tableau[i].Cards.Count(c => !c.IsFaceUp);
		}
		for (int i = 0; i < 4; i++)
			_state.Foundation[i] = _foundations[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		
		_state.Stock = _stockPile.Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		_state.Waste = _wastePile.Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
	}

	private void ApplyState(KlondikeState snap)
	{
		foreach (var card in _cardLookup.Values) card.Visible = false;
		foreach (var p in GetPilesForInput()) while (!p.IsEmpty) p.RemoveTopCard();

		for (int i = 0; i < 7; i++)
		{
			for (int j = 0; j < snap.Tableau[i].Count; j++)
			{
				var cm = snap.Tableau[i][j];
				var card = _cardLookup[(cm.Suit, cm.Rank)];
				card.Visible = true;
				card.IsFaceUp = j >= snap.TableauFaceDown[i];
				_tableau[i].AddCard(card);
			}
		}

		foreach (var cm in snap.Stock)
		{
			var card = _cardLookup[(cm.Suit, cm.Rank)];
			card.Visible = true;
			card.IsFaceUp = false;
			_stockPile.AddCard(card);
		}

		foreach (var cm in snap.Waste)
		{
			var card = _cardLookup[(cm.Suit, cm.Rank)];
			card.Visible = true;
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
		}

		for (int i = 0; i < 4; i++)
		{
			foreach (var cm in snap.Foundation[i])
			{
				var card = _cardLookup[(cm.Suit, cm.Rank)];
				card.Visible = true;
				card.IsFaceUp = true;
				_foundations[i].AddCard(card);
			}
		}
		
		UpdateWastePositions();
		_menu.SetUndoEnabled(_undoStack.Count > 0);
	}

	// ── Rules ──────────────────────────────────────────────────────────────

	protected override bool ShouldAllowDrag(Card card)
	{
		if (card.CurrentPile == _stockPile) return false;
		return card.IsFaceUp;
	}

	protected override bool CanMoveStack(CardPile pile, Card card, int count)
	{
		return (pile.PileType == PileType.Tableau) || (count == 1);
	}

	protected override CardPile? FindDropTarget(Card draggingCard)
	{
		var allPiles = _foundations.Concat(_tableau).ToList();
		return OverlapUtils.GetMostOverlapping(draggingCard.GetGlobalRect(), allPiles, p => p.GetGlobalRect());
	}

	protected override bool CanDropCards(Card bottomCard, List<Card> draggingCards, CardPile target)
	{
		var movingModels = draggingCards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		int targetIdx = -1;
		if (target.PileType == PileType.Tableau) targetIdx = Array.IndexOf(_tableau, target);
		else if (target.PileType == PileType.Foundation) targetIdx = Array.IndexOf(_foundations, target);

		return KlondikeEngine.CanMove(_state, movingModels, target.PileType, targetIdx);
	}

	protected override void ExecuteDrop(CardPile target, List<Card> draggingCards)
	{
		foreach (var c in draggingCards) target.AddCard(c);
	}

	protected override void HandleEmptySpaceClick(Vector2 globalPos)
	{
		if (IsPointInPile(globalPos, _stockPile)) DrawFromStock();
	}

	protected override void HandleCardClick(Card card)
	{
		if (card.CurrentPile == _stockPile) DrawFromStock();
	}

	protected override void OnBeforeDragStarted() => _undoStack.Push(CaptureState());

	protected override void OnDragEnded(bool valid)
	{
		if (!valid) _undoStack.Pop();
		else
		{
			CheckTableauFlip();
			UpdateStateFromPiles();
			SaveGame();
			if (KlondikeEngine.IsWon(_state)) EnterWinState();
		}
		UpdateWastePositions();
	}

	protected override IEnumerable<CardPile> GetPilesForInput() => 
		new[] { _stockPile, _wastePile }.Concat(_foundations).Concat(_tableau);

	// ── Utils ──────────────────────────────────────────────────────────────

	private void CheckTableauFlip()
	{
		foreach (var p in _tableau)
			if (!p.IsEmpty && !p.TopCard!.IsFaceUp) p.TopCard!.IsFaceUp = true;
	}

	private void DrawFromStock()
	{
		_undoStack.Push(CaptureState());
		if (_stockPile.IsEmpty)
		{
			while (!_wastePile.IsEmpty)
			{
				var card = _wastePile.RemoveTopCard();
				if (card != null)
				{
					card.IsFaceUp = false;
					_stockPile.AddCard(card);
				}
			}
		}
		else
		{
			int draw = Math.Min(_state.DrawCount, _stockPile.Count);
			for (int i = 0; i < draw; i++)
			{
				var card = _stockPile.RemoveTopCard();
				if (card != null)
				{
					card.IsFaceUp = true;
					_wastePile.AddCard(card);
				}
			}
		}
		UpdateWastePositions();
		UpdateStateFromPiles();
		SaveGame();
	}

	private void UpdateWastePositions()
	{
		_wastePile.Cascade = CascadeDirection.None; 
		int total = _wastePile.Count;
		for (int i = 0; i < total; i++)
		{
			if (_state.DrawCount == 1)
			{
				_wastePile.Cards[i].Position = Vector2.Zero;
			}
			else
			{
				int offsetIdx = Math.Max(0, i - (total - 3));
				if (total <= 3) offsetIdx = i;
				_wastePile.Cards[i].Position = new Vector2(offsetIdx * _wastePile.CardOffset, 0);
			}
		}
	}

	private KlondikeState CaptureState()
	{
		var snap = new KlondikeState { 
			DrawCount = _state.DrawCount, 
			IsFinished = _gameWon, 
			InitialDeal = _state.InitialDeal.ToList() 
		};
		for (int i = 0; i < 7; i++)
		{
			snap.Tableau[i] = _tableau[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
			snap.TableauFaceDown[i] = _tableau[i].Cards.Count(c => !c.IsFaceUp);
		}
		for (int i = 0; i < 4; i++)
			snap.Foundation[i] = _foundations[i].Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		
		snap.Stock = _stockPile.Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		snap.Waste = _wastePile.Cards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
		return snap;
	}
}
