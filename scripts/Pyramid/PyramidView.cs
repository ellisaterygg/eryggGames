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
	private CardPile _stockPile;
	private CardPile _wastePile;
	private Dictionary<(int r, int c), Card> _pyramidCards = new();
	private PyramidState _state = new();
	private PackedScene _cardScene;

	// Drag state
	private readonly List<Card> _dragCards = new();
	private CardPile? _dragOriginPile;
	private Vector2 _dragOffset;

	protected override void SetupGame()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();

		var saved = SaveManager.LoadGame<PyramidState>("Pyramid");
		if (saved != null && !saved.IsFinished)
		{
			ApplyState(saved);
		}
		else
		{
			NewGame();
		}
	}

	protected override bool ShowUndoButton => false;

	private void SetupPiles()
	{
		var safeOffset = new Vector2(0, _topInset);
		var stockNode = GetNode<Node2D>("Stock");
		_stockPile = new CardPile { Name = "StockPile", PileType = PileType.FreeCell };
		stockNode.AddChild(_stockPile);

		var wasteNode = GetNode<Node2D>("Waste");
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
		var deck = new Deck().Shuffle();
		_state = new PyramidState { InitialDeal = deck.Select(c => new CardModel(c.suit, c.rank)).ToList() };
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
		{
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = null;
		}

		int idx = 0;
		for (int r = 0; r < 7; r++)
		{
			for (int c = 0; c <= r; c++) _state.Pyramid[r][c] = order[idx++];
		}
		while (idx < order.Count) _state.Stock.Add(order[idx++]);

		ApplyState(_state);
		SaveManager.SaveGame("Pyramid", _state);
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
		foreach (var c in _pyramidCards.Values) c.QueueFree();
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

	// ── Input ──────────────────────────────────────────────────────────────

	public override void _Input(InputEvent @event)
	{
		if (_gameWon) return;

		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
				BeginDrag(mb.GlobalPosition);
				break;
			case InputEventMouseMotion mm when _dragCards.Count > 0:
				UpdateDrag(mm.GlobalPosition);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb:
				if (_dragCards.Count > 0) EndDrag(mb.GlobalPosition);
				break;
		}
	}

	private void BeginDrag(Vector2 pos)
	{
		var card = GetCardAt(pos);
		if (card == null)
		{
			if (IsPointInCard(pos, GetNode<Node2D>("Stock").GlobalPosition))
				DrawFromStock();
			return;
		}

		if (card.CurrentPile == _stockPile)
		{
			DrawFromStock();
			return;
		}

		if (!IsCardExposed(card)) return;

		// King logic: remove immediately on click/start of drag
		if (card.Rank == Rank.King)
		{
			RemoveCard(card);
			UpdateGameState();
			return;
		}

		_dragOriginPile = card.CurrentPile;
		var globalPos = card.GlobalPosition;
		
		card.Reparent(this);
		card.Position = globalPos;
		_dragOffset = globalPos - pos;
		card.ZIndex = 100;
		_dragCards.Add(card);
	}

	private void UpdateDrag(Vector2 pos)
	{
		_dragCards[0].Position = pos + _dragOffset;
	}

	private void EndDrag(Vector2 pos)
	{
		var dragCard = _dragCards[0];
		
		// Get all exposed cards that could be a match target
		var candidates = _pyramidCards.Values
			.Concat(new[] { _wastePile.TopCard })
			.Where(c => c != null && c != dragCard && IsCardExposed(c))
			.ToList();

		var targetCard = OverlapUtils.GetMostOverlapping(dragCard.GetGlobalRect(), candidates, c => c.GetGlobalRect());

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

		if (!valid)
		{
			if (_dragOriginPile != null)
			{
				_dragOriginPile.AddCard(dragCard);
			}
			else
			{
				// If it was in the pyramid
				GetNode("PyramidContainer").AddChild(dragCard);
				var pPos = GetPyramidPos(dragCard);
				if (pPos.HasValue) dragCard.Position = GetPyramidPosition(pPos.Value.r, pPos.Value.c);
			}
		}

		_dragCards.Clear();
		_dragOriginPile = null;

		if (valid)
		{
			UpdateGameState();
		}
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
			var model = GetModel(card);
			_state.Waste.Remove(model);
			if (card.CurrentPile == _wastePile) _wastePile.RemoveTopCard();
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

	private Card? GetCardAt(Vector2 pos)
	{
		foreach (var card in _pyramidCards.Values.Reverse())
		{
			if (IsPointInCard(pos, card.GlobalPosition)) return card;
		}
		if (!_wastePile.IsEmpty && IsPointInCard(pos, _wastePile.TopCard.GlobalPosition)) return _wastePile.TopCard;
		if (!_stockPile.IsEmpty && IsPointInCard(pos, _stockPile.TopCard.GlobalPosition)) return _stockPile.TopCard;
		return null;
	}

	private void UpdateGameState()
	{
		UpdateCardVisuals();
		SaveManager.SaveGame("Pyramid", _state);
		if (PyramidEngine.IsWon(_state)) EnterWinState();
	}

	private void UpdateCardVisuals()
	{
		foreach (var kvp in _pyramidCards)
		{
			var card = kvp.Value;
			bool exposed = PyramidEngine.IsExposed(kvp.Key.r, kvp.Key.c, _state);
			card.Modulate = exposed ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
		}
	}

	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;
}
