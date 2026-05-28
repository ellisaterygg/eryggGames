#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.TriPeaks.Core;

namespace EryggGames.TriPeaks;

public partial class TriPeaksView : BaseGameView
{
	private CardPile _stockPile = null!;
	private CardPile _wastePile = null!;
	private Dictionary<(int r, int c), Card> _peaksCards = new();
	private TriPeaksState _state = new();
	private readonly Stack<TriPeaksState> _undoStack = new();
	private TriPeaksState? _pendingSnapshot;
	private PackedScene _cardScene = null!;

	private List<GameOption> _options = new();

	protected override bool ShowUndoButton => true;
	protected override bool IsGameInProgress => _undoStack.Count > 0 && !_gameWon;
	protected override bool CanUndo => _undoStack.Count > 0;

	protected override int EntryCost => 50;
	protected override int WinBonus => _state.WinnableOnly ? 50 : 100;

	private int _currentChain = 0;

	protected override void SetupGame()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();
var saved = SaveManager.LoadGame<TriPeaksState>("TriPeaks");
if (saved != null && !saved.IsFinished)
{
	_state = saved;
	ApplyState(_state);
}
else
{
	if (saved != null) _state.WinnableOnly = saved.WinnableOnly;
	NewGame();
}

		SetupOptionsMenu();
	}

	private void SetupOptionsMenu()
	{
		_options = new List<GameOption>
		{
			new GameOption { 
				Id = "winnable_only", 
				Label = "Deals", 
				Options = new[] { "All Deals", "Winnable Only" }, 
				SelectedIndex = _state.WinnableOnly ? 1 : 0 
			}
		};
		_menu.SetOptions(_options);
	}

	protected override void OnOptionsApplied(bool startNewGame)
	{
		var winOpt = _options.First(o => o.Id == "winnable_only");
		_state.WinnableOnly = winOpt.SelectedIndex == 1;

		if (startNewGame) NewGame();
		else SaveGame();
	}

	protected override void HideCardsAndMarkings()
	{
		base.HideCardsAndMarkings();
		GetNode<Node2D>("PeaksContainer").Visible = false;
	}

	protected override void ShowCardsAndMarkings()
	{
		base.ShowCardsAndMarkings();
		GetNode<Node2D>("PeaksContainer").Visible = true;
	}

	protected override void UndoMove()
	{
		if (_undoStack.Count > 0)
		{
			_state = _undoStack.Pop();
			ApplyState(_state);
			SaveGame();
		}
	}

	private void SetupPiles()
	{
		var safeOffset = new Vector2(0, _topInset);
		var stockNode = GetNode<Node2D>("Stock");
		stockNode.Position += safeOffset;
		_stockPile = new CardPile { Name = "StockPile", PileType = PileType.FreeCell };
		stockNode.AddChild(_stockPile);

		var wasteNode = GetNode<Node2D>("Waste");
		wasteNode.Position += safeOffset;
		_wastePile = new CardPile { Name = "WastePile", PileType = PileType.FreeCell };
		wasteNode.AddChild(_wastePile);

		GetNode<Node2D>("PeaksContainer").Position += safeOffset;
	}

	protected override void NewGame()
	{
		ExitWinState();
		LoadBackground();
		_undoStack.Clear();

		List<CardModel> order;
		int attempts = 0;
		do {
			var deck = new Deck().Shuffle();
			order = deck.Select(c => new CardModel(c.suit, c.rank)).ToList();
			attempts++;

			if (!_state.WinnableOnly) break;

			var tempState = new TriPeaksState();
			int idx = 0;
			for (int r = 0; r < 4; r++)
				for (int c = 0; c < tempState.Peaks[r].Length; c++)
					tempState.Peaks[r][c] = order[idx++];
			
			tempState.Waste.Add(order[idx++]);
			while (idx < order.Count) tempState.Stock.Add(order[idx++]);

			if (TriPeaksSolver.IsWinnable(tempState)) break;

		} while (attempts < 200);

		GD.Print($"Dealt winnable TriPeaks in {attempts} attempts.");
		
		_state.InitialDeal = order;
		DealFromOrder(_state.InitialDeal);
	}

	protected override void RestartGame()
	{
		ExitWinState();
		_undoStack.Clear();
		if (_state.InitialDeal.Count == 0) NewGame();
		else DealFromOrder(_state.InitialDeal);
	}

	private void DealFromOrder(List<CardModel> order)
	{
		_state.Stock.Clear();
		_state.Waste.Clear();
		_state.IsFinished = false;
		_state.BackgroundFile = _currentBackgroundFile;
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < _state.Peaks[i].Length; j++) _state.Peaks[i][j] = null;
		}

		int idx = 0;
		for (int r = 0; r < 4; r++)
		{
			for (int c = 0; c < _state.Peaks[r].Length; c++)
			{
				_state.Peaks[r][c] = order[idx++];
			}
		}

		_state.Waste.Add(order[idx++]);
		while (idx < order.Count) _state.Stock.Add(order[idx++]);

		ApplyState(_state);
		SaveManager.SaveGame("TriPeaks", _state);
	}

	private void ApplyState(TriPeaksState state)
	{
		_state = state;
		LoadBackground(_state.BackgroundFile);
		ClearBoard();

		var container = GetNode("PeaksContainer");
		for (int r = 0; r < 4; r++)
		{
			for (int c = 0; c < _state.Peaks[r].Length; c++)
			{
				var model = _state.Peaks[r][c];
				if (model == null) continue;
				var card = CreateCard(model);
				container.AddChild(card);
				card.Position = GetPeakPosition(r, c);
				card.IsFaceUp = (r == 3); 
				_peaksCards[(r, c)] = card;
			}
		}

		foreach (var model in _state.Stock)
		{
			var card = CreateCard(model);
			card.IsFaceUp = false;
			_stockPile.AddCard(card);
		}

		foreach (var model in _state.Waste)
		{
			var card = CreateCard(model);
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
		}
		
		UpdateCardVisuals();
		_gameWon = _state.IsFinished;
		_menu.SetUndoEnabled(_undoStack.Count > 0);
	}

	private Card CreateCard(CardModel model)
	{
		var card = _cardScene.Instantiate<Card>();
		card.Init(model.Suit, model.Rank);
		return card;
	}

	private void ClearBoard()
	{
		foreach (var c in _peaksCards.Values) if (c != null) c.QueueFree();
		_peaksCards.Clear();
		while (!_stockPile.IsEmpty) _stockPile.RemoveTopCard()?.QueueFree();
		while (!_wastePile.IsEmpty) _wastePile.RemoveTopCard()?.QueueFree();
	}

	private Vector2 GetPeakPosition(int r, int c)
	{
		float vSpacing = Card.CardHeight * 0.88f; 
		float hOverlap = Card.CardWidth * 0.7f;
		float x = (3 - r) * hOverlap; 
		float y = 0;
		if (r == 3) y = (c - 4.5f) * vSpacing;
		else if (r == 2) y = (c - 4.0f) * vSpacing;
		else if (r == 1) {
			int peak = c / 2;
			int sub = c % 2;
			y = (peak * 3 + sub + 0.5f - 4.0f) * vSpacing;
		}
		else y = (c * 3 + 1.0f - 4.0f) * vSpacing;
		return new Vector2(x, y);
	}

	// ── Rules ──────────────────────────────────────────────────────────────

	protected override bool ShouldAllowDrag(Card card)
	{
		return false;
	}

	protected override CardPile? FindDropTarget(Card draggingCard)
	{
		if (draggingCard.GetGlobalRect().Intersects(_wastePile.GetGlobalRect())) return _wastePile;
		return null;
	}

	protected override bool CanDropCards(Card bottomCard, List<Card> draggingCards, CardPile target)
	{
		if (target != _wastePile) return false;
		var wasteTop = _wastePile.TopCard;
		if (wasteTop == null) return true;
		return TriPeaksEngine.IsValidMove(GetModel(bottomCard), GetModel(wasteTop));
	}

	protected override void ExecuteDrop(CardPile target, List<Card> draggingCards)
	{
		if (_pendingSnapshot != null) _undoStack.Push(_pendingSnapshot);
		MoveToWaste(draggingCards[0]);
	}

	protected override void HandleCardClick(Card card)
	{
		if (card.CurrentPile == _stockPile)
		{
			DrawFromStock();
		}
		else if (IsCardExposed(card))
		{
			var wasteTop = _wastePile.TopCard;
			if (wasteTop == null || TriPeaksEngine.IsValidMove(GetModel(card), GetModel(wasteTop)))
			{
				_undoStack.Push(_state.Clone());
				MoveToWaste(card);
			}
		}
	}

	protected override void HandleMouseButtonDoubleClicked(Vector2 globalPos)
	{
		if (_gameWon) return;
		var card = GetCardAt(globalPos);
		if (card != null)
		{
			if (card.CurrentPile == _stockPile) DrawFromStock();
			else HandleCardClick(card);
		}
		else if (IsPointInPile(globalPos, _stockPile))
		{
			DrawFromStock();
		}
	}

	protected override void OnBeforeDragStarted() => _pendingSnapshot = _state.Clone();

	protected override void OnDragEnded(bool valid, CardPile? target) => _pendingSnapshot = null;

	protected override void HandleEmptySpaceClick(Vector2 globalPos)
	{
		if (IsPointInPile(globalPos, _stockPile))
		{
			DrawFromStock();
		}
	}

	protected override void CancelDrag()
	{
		if (_dragCards.Count == 0) return;
		var dragCard = _dragCards[0];
		if (_dragOriginPile != null)
		{
			_dragOriginPile.AddCard(dragCard);
		}
		else
		{
			// Was in the peaks (dictionary managed)
			GetNode("PeaksContainer").AddChild(dragCard);
			var pPos = GetPeakPos(dragCard);
			if (pPos.HasValue) dragCard.Position = GetPeakPosition(pPos.Value.r, pPos.Value.c);
		}
	}

	protected override IEnumerable<CardPile> GetPilesForInput()
	{
		return new[] { _stockPile, _wastePile };
	}

	protected override Card? GetCardAt(Vector2 globalPos)
	{
		foreach (var card in _peaksCards.Values.Reverse())
		{
			if (card != null && IsPointInCard(globalPos, card.GlobalPosition)) return card;
		}
		if (!_wastePile.IsEmpty && IsPointInCard(globalPos, _wastePile.TopCard!.GlobalPosition)) return _wastePile.TopCard;
		if (!_stockPile.IsEmpty && IsPointInCard(globalPos, _stockPile.TopCard!.GlobalPosition)) return _stockPile.TopCard;
		return null;
	}

	private void DrawFromStock()
	{
		if (_state.Stock.Count > 0)
		{
			_currentChain = 0;
			_undoStack.Push(_state.Clone());
			var model = _state.Stock[^1];
			_state.Stock.RemoveAt(_state.Stock.Count - 1);
			_state.Waste.Add(model);
			var card = _stockPile.RemoveTopCard()!;
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
			UpdateGameState();
		}
	}

	private void MoveToWaste(Card card)
	{
		var pPos = GetPeakPos(card);
		if (pPos.HasValue)
		{
			_currentChain++;
			RewardPoints(_currentChain);
			_state.Peaks[pPos.Value.r][pPos.Value.c] = null;
			_peaksCards.Remove(pPos.Value);
			var model = GetModel(card);
			_state.Waste.Add(model);
			_wastePile.AddCard(card);
			card.IsFaceUp = true;
			UpdateGameState();
		}
	}

	private bool IsCardExposed(Card card)
	{
		if (card.CurrentPile == _wastePile) return card == _wastePile.TopCard;
		if (card.CurrentPile == _stockPile) return card == _stockPile.TopCard;
		var pos = GetPeakPos(card);
		if (pos == null) return false;
		return TriPeaksEngine.IsExposed(pos.Value.r, pos.Value.c, _state);
	}

	private (int r, int c)? GetPeakPos(Card card)
	{
		foreach (var kvp in _peaksCards) if (kvp.Value == card) return kvp.Key;
		return null;
	}

	private CardModel GetModel(Card card) => new CardModel(card.Suit, card.Rank);

	private void UpdateGameState()
	{
		UpdateCardVisuals();
		if (TriPeaksEngine.IsWon(_state)) _state.IsFinished = true;
		SaveGame();
		if (_state.IsFinished) EnterWinState();
	}

	private void SaveGame()
	{
		SaveManager.SaveGame("TriPeaks", _state);
		_menu.SetUndoEnabled(_undoStack.Count > 0);
	}

	private void UpdateCardVisuals()
	{
		foreach (var kvp in _peaksCards)
		{
			var (r, c) = kvp.Key;
			var card = kvp.Value;
			if (card == null) continue;
			bool exposed = TriPeaksEngine.IsExposed(r, c, _state);
			if (exposed && !card.IsFaceUp) card.IsFaceUp = true;
			card.Modulate = exposed ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
		}
	}
}
