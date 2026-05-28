#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.Pyramid.Core;

namespace EryggGames.Pyramid;

public partial class PyramidView : BaseGameView
{
	private CardPile _stockPile = null!;
	private CardPile _wastePile = null!;
	private Label _redealLabel = null!;
	private Dictionary<(int r, int c), Card> _pyramidCards = new();
	private PyramidState _state = new();
	private readonly Stack<PyramidState> _undoStack = new();
	private PyramidState? _pendingSnapshot;
	private PackedScene _cardScene = null!;
	private const int MaxRedeals = 2;

	private List<GameOption> _options = new();

	protected override bool ShowUndoButton => true;
	protected override bool IsGameInProgress => _undoStack.Count > 0 && !_gameWon;
	protected override bool CanUndo => _undoStack.Count > 0;

	protected override int EntryCost => 50;
	protected override int WinBonus => _state.WinnableOnly ? 50 : 150;

	protected override void SetupGame()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();

		var saved = SaveManager.LoadGame<PyramidState>("Pyramid");
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
		GetNode<Node2D>("PyramidContainer").Visible = false;
		_redealLabel.Visible = false;
	}

	protected override void ShowCardsAndMarkings()
	{
		base.ShowCardsAndMarkings();
		GetNode<Node2D>("PyramidContainer").Visible = true;
		_redealLabel.Visible = true;
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
		_stockPile = new CardPile { Name = "StockPile", PileType = PileType.FreeCell };
		stockNode.AddChild(_stockPile);

		var wasteNode = GetNode<Node2D>("Waste");
		_wastePile = new CardPile { Name = "WastePile", PileType = PileType.FreeCell };
		wasteNode.AddChild(_wastePile);
		
		stockNode.Position = new Vector2(200, 950) + safeOffset;
		wasteNode.Position = new Vector2(400, 950) + safeOffset;

		_redealLabel = new Label { 
			Position = new Vector2(120, 1020) + safeOffset,
			HorizontalAlignment = HorizontalAlignment.Center,
			Size = new Vector2(160, 40)
		};
		_redealLabel.AddThemeFontSizeOverride("font_size", 22);
		AddChild(_redealLabel);

		GetNode<Node2D>("PyramidContainer").Position += safeOffset;
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

			var tempState = new PyramidState();
			int idx = 0;
			for (int r = 0; r < 7; r++)
				for (int c = 0; c <= r; c++) tempState.Pyramid[r][c] = order[idx++];
			while (idx < order.Count) tempState.Stock.Add(order[idx++]);

			if (PyramidSolver.IsWinnable(tempState)) break;

		} while (attempts < 100);

		_state = new PyramidState { 
			InitialDeal = order,
			WinnableOnly = _state.WinnableOnly 
		};
		DealFromOrder(_state.InitialDeal);
		SaveGame();
	}

	protected override void RestartGame()
	{
		ExitWinState();
		_undoStack.Clear();
		if (_state.InitialDeal.Count == 0) NewGame();
		else DealFromOrder(_state.InitialDeal);
		SaveGame();
	}

	private void DealFromOrder(List<CardModel> order)
	{
		_state.Stock.Clear();
		_state.Waste.Clear();
		_state.DeckPasses = 0;
		_state.IsFinished = false;
		_state.BackgroundFile = _currentBackgroundFile;
		for (int r = 0; r < 7; r++)
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = null;

		int idx = 0;
		for (int r = 0; r < 7; r++)
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = order[idx++];
		while (idx < order.Count) _state.Stock.Add(order[idx++]);

		ApplyState(_state);
	}

	private void ApplyState(PyramidState state)
	{
		_state = state;
		ClearBoard();

		var container = GetNode("PyramidContainer");
		for (int r = 0; r < 7; r++)
		{
			for (int c = 0; c <= r; c++)
			{
				var model = _state.Pyramid[r][c];
				if (model == null) continue;
				var card = CreateCard(model);
				container.AddChild(card);
				card.Position = GetPyramidPosition(r, c);
				card.IsFaceUp = true;
				_pyramidCards[(r, c)] = card;
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
		foreach (var c in _pyramidCards.Values) if (c != null) c.QueueFree();
		_pyramidCards.Clear();
		while (!_stockPile.IsEmpty) _stockPile.RemoveTopCard()?.QueueFree();
		while (!_wastePile.IsEmpty) _wastePile.RemoveTopCard()?.QueueFree();
	}

	private Vector2 GetPyramidPosition(int r, int c)
	{
		float hSpacing = Card.CardWidth * 1.1f;
		float vSpacing = Card.CardHeight * 0.4f;
		float rowStartX = -(r * hSpacing) / 2f;
		return new Vector2(rowStartX + c * hSpacing, r * vSpacing);
	}

	// ── Rules ──────────────────────────────────────────────────────────────

	protected override bool ShouldAllowDrag(Card card)
	{
		if (card.Rank == Rank.King) return true; // King removes self
		if (card.CurrentPile == _stockPile && card != _stockPile.TopCard) return false;
		return IsCardExposed(card);
	}

	protected override CardPile? FindDropTarget(Card draggingCard) => null; // Manual handling in EndDrag

	protected override bool CanDropCards(Card bottomCard, List<Card> draggingCards, CardPile target) => false;

	protected override void ExecuteDrop(CardPile target, List<Card> draggingCards) { }

	protected override void HandleEmptySpaceClick(Vector2 globalPos)
	{
		if (IsPointInPile(globalPos, _stockPile)) DrawFromStock();
	}

	protected override void HandleCardClick(Card card)
	{
		if (_gameWon) return;

		if (!IsCardExposed(card)) return;

		if (card.Rank == Rank.King)
		{
			_undoStack.Push(_state.Clone());
			RemoveCard(card);
			UpdateGameState();
			return;
		}

		if (card.CurrentPile == _stockPile)
		{
			DrawFromStock();
		}
	}

	protected override void HandleMouseButtonDoubleClicked(Vector2 globalPos)
	{
		if (_gameWon) return;
		var card = GetCardAt(globalPos);
		if (card != null)
		{
			HandleCardClick(card);
		}
		else if (IsPointInPile(globalPos, _stockPile))
		{
			DrawFromStock();
		}
	}

	protected override void OnBeforeDragStarted()
	{
		_pendingSnapshot = _state.Clone();
	}

	protected override void OnDragEnded(bool valid, CardPile? target) => _pendingSnapshot = null;

	protected override void BeginDrag(Card card, Vector2 globalMousePos)
	{
		if (!IsCardExposed(card)) return;

		if (card.Rank == Rank.King)
		{
			_undoStack.Push(_state.Clone());
			RemoveCard(card);
			UpdateGameState();
			return;
		}
		base.BeginDrag(card, globalMousePos);
	}

	protected override void EndDrag(Vector2 globalMousePos)
	{
		if (_dragCards.Count == 0) return;
		var dragCard = _dragCards[0];
		
		var candidates = _pyramidCards.Values
			.Concat(new[] { _wastePile.TopCard, _stockPile.TopCard })
			.Where(c => c != null && c != dragCard && IsCardExposed(c))
			.ToList();

		var targetCard = OverlapUtils.GetMostOverlapping(dragCard.GetGlobalRect(), candidates!, c => c.GetGlobalRect());

		bool valid = false;
		if (targetCard != null)
		{
			if ((int)dragCard.Rank + (int)targetCard.Rank == 13)
			{
				if (_pendingSnapshot != null) _undoStack.Push(_pendingSnapshot);
				RemoveCard(targetCard);
				RemoveCard(dragCard);
				valid = true;
			}
		}

		if (!valid)
		{
			CancelDrag();
		}

		_dragCards.Clear();
		_dragOriginPile = null;
		_dragOffsets = Array.Empty<Vector2>();

		if (valid) UpdateGameState();
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
			// Was in the pyramid (dictionary managed)
			var container = GetNode<Node2D>("PyramidContainer");
			if (dragCard.GetParent() != container)
			{
				dragCard.GetParent()?.RemoveChild(dragCard);
				container.AddChild(dragCard);
			}
			
			var pPos = GetPyramidPos(dragCard);
			if (pPos.HasValue) dragCard.Position = GetPyramidPosition(pPos.Value.r, pPos.Value.c);
		}
	}

	protected override IEnumerable<CardPile> GetPilesForInput() => 
		new[] { _stockPile, _wastePile }; 

	protected override Card? GetCardAt(Vector2 globalPos)
	{
		foreach (var card in _pyramidCards.Values.Reverse())
			if (card != null && IsPointInCard(globalPos, card.GlobalPosition)) return card;
		if (!_wastePile.IsEmpty && IsPointInCard(globalPos, _wastePile.TopCard!.GlobalPosition)) return _wastePile.TopCard;
		if (!_stockPile.IsEmpty && IsPointInCard(globalPos, _stockPile.TopCard!.GlobalPosition)) return _stockPile.TopCard;
		return null;
	}

	private void RemoveCard(Card card)
	{
		RewardPoints(1);
		var pos = GetPyramidPos(card);
		if (pos.HasValue)
		{
			_state.Pyramid[pos.Value.r][pos.Value.c] = null;
			_pyramidCards.Remove(pos.Value);
			card.QueueFree();
		}
		else
		{
			// Check if it's in a pile
			var pile = card.CurrentPile;
			if (pile == _wastePile)
			{
				if (_state.Waste.Count > 0) _state.Waste.RemoveAt(_state.Waste.Count - 1);
				pile.Cards.Remove(card);
				card.QueueFree();
			}
			else if (pile == _stockPile)
			{
				if (_state.Stock.Count > 0) _state.Stock.RemoveAt(_state.Stock.Count - 1);
				pile.Cards.Remove(card);
				card.QueueFree();
			}
			else if (_dragCards.Contains(card))
			{
				if (_dragOriginPile == _wastePile)
				{
					if (_state.Waste.Count > 0) _state.Waste.RemoveAt(_state.Waste.Count - 1);
				}
				else if (_dragOriginPile == _stockPile)
				{
					if (_state.Stock.Count > 0) _state.Stock.RemoveAt(_state.Stock.Count - 1);
				}
				card.QueueFree();
			}
			else
			{
				card.QueueFree();
			}
		}
	}

	private void DrawFromStock()
	{
		if (_state.Stock.Count > 0)
		{
			_undoStack.Push(_state.Clone());
			var model = _state.Stock[^1];
			_state.Stock.RemoveAt(_state.Stock.Count - 1);
			_state.Waste.Add(model);
			
			var card = _stockPile.RemoveTopCard();
			if (card == null)
			{
				UpdateGameState();
				return;
			}

			card.IsFaceUp = true;
			_wastePile.AddCard(card);
			UpdateGameState();
		}
		else if (_state.DeckPasses < MaxRedeals)
		{
			_undoStack.Push(_state.Clone());
			_state.DeckPasses++;
			
			while (_state.Waste.Count > 0)
			{
				var m = _state.Waste[^1];
				_state.Waste.RemoveAt(_state.Waste.Count - 1);
				_state.Stock.Add(m);
				var c = _wastePile.RemoveTopCard();
				if (c != null)
				{
					c.IsFaceUp = false;
					_stockPile.AddCard(c);
				}
			}

			UpdateGameState();
		}
	}

	private bool IsCardExposed(Card card)
	{
		if (card.CurrentPile == _wastePile) return card == _wastePile.TopCard;
		if (card.CurrentPile == _stockPile) return card == _stockPile.TopCard;
		var pos = GetPyramidPos(card);
		if (pos == null) return false;
		return PyramidEngine.IsExposed(pos.Value.r, pos.Value.c, _state);
	}

	private (int r, int c)? GetPyramidPos(Card card)
	{
		foreach (var kvp in _pyramidCards) if (kvp.Value == card) return kvp.Key;
		return null;
	}

	private CardModel GetModel(Card card) => new CardModel(card.Suit, card.Rank);

	private void UpdateGameState()
	{
		UpdateCardVisuals();
		_redealLabel.Text = $"Redeals: {MaxRedeals - _state.DeckPasses}";
		
		if (PyramidEngine.IsWon(_state))
		{
			_state.IsFinished = true;
			SaveGame();
			EnterWinState();
		}
		else
		{
			SaveGame();
		}
	}

	private void SaveGame()
	{
		SaveManager.SaveGame("Pyramid", _state);
		_menu.SetUndoEnabled(_undoStack.Count > 0);
	}

	private void UpdateCardVisuals()
	{
		foreach (var kvp in _pyramidCards)
		{
			var card = kvp.Value;
			if (card == null) continue;
			bool exposed = PyramidEngine.IsExposed(kvp.Key.r, kvp.Key.c, _state);
			card.Modulate = exposed ? new Color(1, 1, 1) : new Color(0.7f, 0.7f, 0.7f);
		}
		
		if (!_stockPile.IsEmpty) 
		{
			_stockPile.TopCard!.IsFaceUp = true;
			_stockPile.TopCard.Modulate = new Color(1, 1, 1);
		}
		
		if (!_wastePile.IsEmpty)
		{
			_wastePile.TopCard!.Modulate = new Color(1, 1, 1);
		}
	}
}
