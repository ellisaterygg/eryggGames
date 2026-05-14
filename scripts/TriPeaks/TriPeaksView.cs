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
	private CardPile _stockPile;
	private CardPile _wastePile;
	private Dictionary<(int r, int c), Card> _peaksCards = new();
	private TriPeaksState _state = new();
	private PackedScene _cardScene;

	protected override void SetupGame()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		SetupPiles();

		var saved = SaveManager.LoadGame<TriPeaksState>("TriPeaks");
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
		var deck = new Deck().Shuffle();
		_state = new TriPeaksState { InitialDeal = deck.Select(c => new CardModel(c.suit, c.rank)).ToList() };
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
	}

	private Card CreateCard(CardModel model)
	{
		var card = _cardScene.Instantiate<Card>();
		card.Init(model.Suit, model.Rank);
		return card;
	}

	private void ClearBoard()
	{
		foreach (var c in _peaksCards.Values) c.QueueFree();
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

	public override void _Input(InputEvent @event)
	{
		if (_gameWon) return;
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
		{
			HandleClick(mb.GlobalPosition);
		}
	}

	private void HandleClick(Vector2 pos)
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

		if (TriPeaksEngine.IsValidMove(GetModel(card), GetModel(_wastePile.TopCard)))
		{
			MoveToWaste(card);
			UpdateGameState();
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
	}

	private void MoveToWaste(Card card)
	{
		var pPos = GetPyramidPos(card);
		if (pPos.HasValue)
		{
			_state.Peaks[pPos.Value.r][pPos.Value.c] = null;
			_peaksCards.Remove(pPos.Value);
			var model = GetModel(card);
			_state.Waste.Add(model);
			card.Reparent(_wastePile);
			_wastePile.AddCard(card);
			card.IsFaceUp = true;
		}
	}

	private bool IsCardExposed(Card card)
	{
		if (card.CurrentPile == _wastePile) return card == _wastePile.TopCard;
		if (card.CurrentPile == _stockPile) return card == _stockPile.TopCard;
		var pos = GetPyramidPos(card);
		if (pos == null) return false;
		return TriPeaksEngine.IsExposed(pos.Value.r, pos.Value.c, _state);
	}

	private (int r, int c)? GetPyramidPos(Card card)
	{
		foreach (var kvp in _peaksCards) if (kvp.Value == card) return kvp.Key;
		return null;
	}

	private CardModel GetModel(Card card) => new CardModel(card.Suit, card.Rank);

	private Card? GetCardAt(Vector2 pos)
	{
		foreach (var card in _peaksCards.Values.Reverse())
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
		SaveManager.SaveGame("TriPeaks", _state);
		if (TriPeaksEngine.IsWon(_state)) EnterWinState();
	}

	private void UpdateCardVisuals()
	{
		foreach (var kvp in _peaksCards)
		{
			var (r, c) = kvp.Key;
			var card = kvp.Value;
			bool exposed = TriPeaksEngine.IsExposed(r, c, _state);
			if (exposed && !card.IsFaceUp) card.IsFaceUp = true;
			card.Modulate = exposed ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
		}
	}

	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;
}
