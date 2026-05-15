#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.Pyramid.Core;
using EryggGames.Pyramid.Tests;

namespace EryggGames.Pyramid;

public partial class PyramidView : BaseGameView
{
	private CardPile _stockPile = null!;
	private CardPile _wastePile = null!;
	private Dictionary<(int r, int c), Card> _pyramidCards = new();
	private PyramidState _state = new();
	private PackedScene _cardScene = null!;

	private List<GameOption> _options = new();

	protected override void SetupGame()
	{
#if DEBUG
		SolverTests.RunTests();
#endif
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
		else SaveManager.SaveGame("Pyramid", _state);
	}

	protected override bool ShowUndoButton => false;

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
		
		stockNode.Position = new Vector2(200, 1100) + safeOffset;
		wasteNode.Position = new Vector2(400, 1100) + safeOffset;

		GetNode<Node2D>("PyramidContainer").Position += safeOffset;
	}

	protected override void NewGame()
	{
		ExitWinState();
		LoadBackground();

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

		} while (attempts < 200);

		GD.Print($"Dealt winnable game in {attempts} attempts.");
		
		_state.InitialDeal = order;
		DealFromOrder(_state.InitialDeal);
	}

	protected override void RestartGame()
	{
		ExitWinState();
		if (_state.InitialDeal.Count == 0) NewGame();
		else DealFromOrder(_state.InitialDeal);
	}

	private void DealFromOrder(List<CardModel> order)
	{
		_state.Stock.Clear();
		_state.Waste.Clear();
		_state.DeckPasses = 0;
		for (int r = 0; r < 7; r++)
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = null;

		int idx = 0;
		for (int r = 0; r < 7; r++)
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = order[idx++];
		while (idx < order.Count) _state.Stock.Add(order[idx++]);

		ApplyState(_state);
		SaveGame();
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
		if (card.CurrentPile == _stockPile) DrawFromStock();
		else if (card.Rank == Rank.King && IsCardExposed(card))
		{
			RemoveCard(card);
			UpdateGameState();
		}
		else if (IsCardExposed(card))
		{
			// Tap matching logic
			if (_selectedCard == null)
			{
				_selectedCard = card;
				card.Modulate = new Color(0.7f, 1f, 0.7f);
			}
			else if (_selectedCard == card)
			{
				_selectedCard = null;
				card.Modulate = Colors.White;
			}
			else
			{
				if ((int)_selectedCard.Rank + (int)card.Rank == 13)
				{
					RemoveCard(_selectedCard);
					RemoveCard(card);
					_selectedCard = null;
					UpdateGameState();
				}
				else
				{
					_selectedCard.Modulate = Colors.White;
					_selectedCard = card;
					card.Modulate = new Color(0.7f, 1f, 0.7f);
				}
			}
		}
	}

	private Card? _selectedCard;

	protected override void BeginDrag(Card card, Vector2 globalMousePos)
	{
		if (card.Rank == Rank.King && IsCardExposed(card))
		{
			RemoveCard(card);
			UpdateGameState();
			return;
		}
		base.BeginDrag(card, globalMousePos);
	}

	protected override void EndDrag(Vector2 globalMousePos)
	{
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
				RemoveCard(dragCard);
				RemoveCard(targetCard);
				valid = true;
			}
		}

		if (!valid) CancelDrag();

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
			GetNode("PyramidContainer").AddChild(dragCard);
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
		var pos = GetPyramidPos(card);
		if (pos.HasValue)
		{
			_state.Pyramid[pos.Value.r][pos.Value.c] = null;
			_pyramidCards.Remove(pos.Value);
			card.QueueFree();
		}
		else if (card.CurrentPile == _wastePile || _dragOriginPile == _wastePile)
		{
			_state.Waste.Remove(GetModel(card));
			if (card.CurrentPile == _wastePile) _wastePile.RemoveTopCard();
			card.QueueFree();
		}
		else if (card.CurrentPile == _stockPile || _dragOriginPile == _stockPile)
		{
			_state.Stock.Remove(GetModel(card));
			if (card.CurrentPile == _stockPile) _stockPile.RemoveTopCard();
			card.QueueFree();
		}
	}

	private void DrawFromStock()
	{
		if (_state.Stock.Count > 0)
		{
			var model = _state.Stock[^1];
			_state.Stock.RemoveAt(_state.Stock.Count - 1);
			_state.Waste.Add(model);
			var card = _stockPile.RemoveTopCard()!;
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
			UpdateGameState();
		}
		else if (_state.DeckPasses < 2)
		{
			_state.DeckPasses++;
			while (_state.Waste.Count > 0)
			{
				var m = _state.Waste[^1];
				_state.Waste.RemoveAt(_state.Waste.Count - 1);
				_state.Stock.Add(m);
				var c = _wastePile.RemoveTopCard()!;
				c.IsFaceUp = false;
				_stockPile.AddCard(c);
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
		SaveGame();
		if (PyramidEngine.IsWon(_state)) EnterWinState();
	}

	private void SaveGame() => SaveManager.SaveGame("Pyramid", _state);

	private void UpdateCardVisuals()
	{
		foreach (var kvp in _pyramidCards)
		{
			var card = kvp.Value;
			if (card == null) continue;
			bool exposed = PyramidEngine.IsExposed(kvp.Key.r, kvp.Key.c, _state);
			card.Modulate = exposed ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
		}
		if (!_stockPile.IsEmpty) _stockPile.TopCard!.IsFaceUp = true;
	}
}
