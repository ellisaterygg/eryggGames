#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Shared;
using EryggGames.Pyramid.Core;

namespace EryggGames.Pyramid;

public partial class PyramidView : Node2D
{
	private PyramidState _state = new();
	private PackedScene _cardScene = null!;
	
	private readonly Dictionary<(int r, int c), Card> _pyramidCards = new();
	private CardPile _stockPile = null!;
	private CardPile _wastePile = null!;

	private Card? _selectedCard;
	private float _topInset;
	
	// Drag state
	private Card?   _dragCard;
	private Vector2 _dragOffset;
	private Vector2 _dragStartPos;
	private Vector2 _dragMouseStartPos;
	private bool    _gameWon;
	private CanvasLayer? _winOverlay;

	public override void _Ready()
	{
		_cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
		_topInset = GetTopSafeInset();
		
		GetNode<Node2D>("PyramidContainer").Position += new Vector2(0, _topInset);
		
		SetupPiles();
		SetupMenu();
		
		var saved = SaveManager.LoadGame<PyramidState>("Pyramid");
		if (saved != null)
		{
			ApplyState(saved);
			LoadBackground(); 
		}
		else
		{
			NewGame();
		}
	}

	private float GetTopSafeInset()
	{
		var screenH = (float)DisplayServer.ScreenGetSize().Y;
		var safeTopPx = (float)DisplayServer.GetDisplaySafeArea().Position.Y;
		if (safeTopPx <= 0f || screenH <= 0f) return 0f;
		return safeTopPx / screenH * GetViewport().GetVisibleRect().Size.Y;
	}

	private void SetupPiles()
	{
		var stockNode = GetNode<Node2D>("Stock");
		_stockPile = new CardPile { Name = "StockPile", PileType = PileType.FreeCell };
		stockNode.AddChild(_stockPile);

		var wasteNode = GetNode<Node2D>("Waste");
		_wastePile = new CardPile { Name = "WastePile", PileType = PileType.FreeCell };
		wasteNode.AddChild(_wastePile);
		
		stockNode.Position = new Vector2(200, 1100);
		wasteNode.Position = new Vector2(400, 1100);
	}

	private void SetupMenu()
	{
		var layer = new CanvasLayer();
		AddChild(layer);
		float barH = 95f + _topInset;
		var bar = new ColorRect { Color = new Color(0, 0, 0, 0.5f), Size = new Vector2(720, barH) };
		layer.AddChild(bar);

		float btnY = _topInset + 22f;
		bar.AddChild(MakeMenuButton("New",     new Vector2(30,  btnY), NewGame));
		bar.AddChild(MakeMenuButton("Restart", new Vector2(200, btnY), RestartGame));
		bar.AddChild(MakeMenuButton("Games",   new Vector2(540, btnY), ShowGameSelection));
	}

	private Button MakeMenuButton(string text, Vector2 pos, Action handler)
	{
		var btn = new Button { Text = text, Position = pos, Size = new Vector2(130, 52) };
		btn.AddThemeFontSizeOverride("font_size", 20);
		btn.Pressed += handler;
		return btn;
	}

	private void ShowGameSelection()
	{
		GetParent<Launcher>()?.SwitchGame("Launcher");
	}

	private void LoadBackground()
	{
		BackgroundManager.LoadRandomBackground(GetNode<Sprite2D>("Background"));
	}

	private void NewGame()
	{
		ExitWinState();
		LoadBackground();
		var deck = new Deck().Shuffle();
		_state = new PyramidState { InitialDeal = deck.Select(c => new CardModel(c.suit, c.rank)).ToList() };
		DealFromOrder(_state.InitialDeal);
	}

	private void RestartGame()
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
				_pyramidCards[(r, c)] = card;
			}
		}

		foreach (var model in _state.Stock)
		{
			var card = CreateCard(model);
			card.IsFaceUp = true; // Fix: Show the cards so player knows they can match
			_stockPile.AddCard(card);
		}

		foreach (var model in _state.Waste)
		{
			var card = CreateCard(model);
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
		}
		
		UpdateCardVisuals();
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
		_selectedCard = null;
	}

	private Vector2 GetPyramidPosition(int r, int c)
	{
		float xOffset = (c - r / 2.0f) * (Card.CardWidth + 10);
		float yOffset = r * (Card.CardHeight * 0.45f);
		return new Vector2(xOffset, yOffset);
	}

	// ── Input ──────────────────────────────────────────────────────────────

	public override void _Input(InputEvent @event)
	{
		if (_gameWon) return;

		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
				BeginDrag(mb.Position);
				break;
			case InputEventMouseMotion mm when _dragCard != null:
				UpdateDrag(mm.Position);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb:
				if (_dragCard != null) EndDrag(mb.Position);
				break;
		}
	}

	private void BeginDrag(Vector2 pos)
	{
		_dragMouseStartPos = pos;
		var card = GetCardAt(pos);
		if (card == null)
		{
			if (IsPointInCard(pos, GetNode<Node2D>("Stock").GlobalPosition))
				DrawFromStock();
			return;
		}

		if (!IsCardExposed(card)) return;

		_dragCard = card;
		_dragStartPos = card.GlobalPosition;
		_dragOffset = card.GlobalPosition - pos;
		card.ZIndex = 200;
		card.Modulate = new Color(0.7f, 1f, 0.7f);
	}

	private void UpdateDrag(Vector2 pos)
	{
		if (_dragCard != null)
			_dragCard.GlobalPosition = pos + _dragOffset;
	}

	private void EndDrag(Vector2 pos)
	{
	    if (_dragCard == null) return;

	    if (pos.DistanceTo(_dragMouseStartPos) < 10f) 
	    {
	         var cardToTap = _dragCard;
	         _dragCard = null; 
	         HandleTap(cardToTap);
	         return;
	    }

	    if (_dragCard.Rank == Rank.King)
	    {
	        RemoveCard(_dragCard);
	        UpdateGameState();
	        _dragCard = null;
	        return;
	    }

	    // --- NEW AREA-BASED HIT DETECTION ---
	    var candidates = _pyramidCards.Values.Concat(_stockPile.IsEmpty ? new Card[0] : new[] { _stockPile.TopCard })
	                                         .Concat(_wastePile.IsEmpty ? new Card[0] : new[] { _wastePile.TopCard })
	                                         .Where(c => c != _dragCard && IsCardExposed(c))
	                                         .ToList();

	    var targetCard = OverlapUtils.GetMostOverlapping(_dragCard.GetGlobalRect(), candidates, c => c.GetGlobalRect());
	    bool matched = false;

	    if (targetCard != null)
	    {
	        if (PyramidEngine.IsValidPair(GetModel(_dragCard), GetModel(targetCard)))
	        {
	            RemoveCard(_dragCard);
	            RemoveCard(targetCard);
	            matched = true;
	        }
	    }
		if (!matched)
		{
			_dragCard.GlobalPosition = _dragStartPos;
			_dragCard.ZIndex = (_dragCard.GetParent() as CardPile)?.Cards.IndexOf(_dragCard) ?? 0;
			if (_dragCard != _selectedCard) _dragCard.Modulate = Colors.White;
		}
		else
		{
			UpdateGameState();
		}

		_dragCard = null;
	}

	private void HandleTap(Card card)
	{
		if (card.CurrentPile == _stockPile)
		{
			DrawFromStock();
			return;
		}

		if (card.Rank == Rank.King)
		{
			RemoveCard(card);
			_selectedCard = null;
			UpdateGameState();
		}
		else if (_selectedCard == null)
		{
			_selectedCard = card;
			card.Modulate = new Color(0.7f, 1f, 0.7f);
		}
		else if (_selectedCard == card)
		{
			_selectedCard.Modulate = Colors.White;
			_selectedCard = null;
		}
		else
		{
			if (PyramidEngine.IsValidPair(GetModel(_selectedCard), GetModel(card)))
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
		
		if (_dragCard == card) 
		{
			_dragCard.GlobalPosition = _dragStartPos;
			_dragCard = null;
		}
	}

	private void DrawFromStock()
	{
		if (_state.Stock.Count > 0)
		{
			if (_selectedCard != null && IsPyramidCard(_selectedCard))
			{
				var stockModel = _state.Stock[^1];
				if (PyramidEngine.IsValidPair(GetModel(_selectedCard), stockModel))
				{
					RemoveCard(_selectedCard);
					_state.Stock.RemoveAt(_state.Stock.Count - 1);
					_stockPile.RemoveTopCard()?.QueueFree();
					_selectedCard = null;
					UpdateGameState();
					return;
				}
			}

			var model = _state.Stock[^1];
			_state.Stock.RemoveAt(_state.Stock.Count - 1);
			_state.Waste.Add(model);
			
			var card = _stockPile.RemoveTopCard()!;
			card.IsFaceUp = true;
			_wastePile.AddCard(card);
		}
		else if (_state.DeckPasses < 2)
		{
			_state.DeckPasses++;
			_state.Stock = _state.Waste.AsEnumerable().Reverse().ToList();
			_state.Waste.Clear();
			ApplyState(_state);
		}
		UpdateGameState();
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private bool IsCardExposed(Card card)
	{
		if (card.CurrentPile == _stockPile) return card == _stockPile.TopCard;
		if (card.CurrentPile == _wastePile) return card == _wastePile.TopCard;
		
		var pos = GetPyramidPos(card);
		if (pos == null) return false;
		return PyramidEngine.IsExposed(pos.Value.r, pos.Value.c, _state);
	}

	private (int r, int c)? GetPyramidPos(Card card)
	{
		foreach (var kvp in _pyramidCards) if (kvp.Value == card) return kvp.Key;
		return null;
	}

	private bool IsPyramidCard(Card card) => GetPyramidPos(card) != null;

	private CardModel GetModel(Card card) => new CardModel(card.Suit, card.Rank);

	private void RemoveCard(Card card)
	{
		var pPos = GetPyramidPos(card);
		if (pPos.HasValue)
		{
			_state.Pyramid[pPos.Value.r][pPos.Value.c] = null;
			_pyramidCards.Remove(pPos.Value);
		}
		else if (card.CurrentPile == _stockPile)
		{
			_state.Stock.RemoveAt(_state.Stock.Count - 1);
			_stockPile.RemoveTopCard();
		}
		else if (card.CurrentPile == _wastePile)
		{
			_state.Waste.RemoveAt(_state.Waste.Count - 1);
			_wastePile.RemoveTopCard();
		}
		card.QueueFree();
	}

	private Card? GetCardAt(Vector2 pos, Card? ignoreCard = null, bool generous = false)
	{
		foreach (var card in _pyramidCards.Values.Reverse())
		{
			if (card == ignoreCard) continue;
			bool hit = generous ? IsPointInDropZone(pos, card.GlobalPosition) : IsPointInCard(pos, card.GlobalPosition);
			if (hit) return card;
		}

		if (!_wastePile.IsEmpty && _wastePile.TopCard != ignoreCard)
		{
			bool hit = generous ? IsPointInDropZone(pos, _wastePile.TopCard.GlobalPosition) : IsPointInCard(pos, _wastePile.TopCard.GlobalPosition);
			if (hit) return _wastePile.TopCard;
		}

		if (!_stockPile.IsEmpty && _stockPile.TopCard != ignoreCard)
		{
			bool hit = generous ? IsPointInDropZone(pos, _stockPile.TopCard.GlobalPosition) : IsPointInCard(pos, _stockPile.TopCard.GlobalPosition);
			if (hit) return _stockPile.TopCard;
		}
		
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
		// Pyramid cards: White if exposed, Dimmed if covered
		foreach (var card in _pyramidCards.Values)
			card.Modulate = IsCardExposed(card) ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
		
		// Stock cards: Reset to white
		foreach (var card in _stockPile.Cards)
			card.Modulate = Colors.White;

		// Waste cards: Reset to white
		foreach (var card in _wastePile.Cards)
			card.Modulate = Colors.White;

		// Apply selection highlight
		if (_selectedCard != null) 
			_selectedCard.Modulate = new Color(0.7f, 1f, 0.7f);
	}

	private void EnterWinState()
	{
		_gameWon = true;
		_winOverlay = new CanvasLayer { Layer = 5 };
		AddChild(_winOverlay);
		var vpSize = GetViewport().GetVisibleRect().Size;
		var band = new ColorRect { Color = new Color(0, 0, 0, 0.45f), Position = new Vector2(0, vpSize.Y / 2 - 50), Size = new Vector2(vpSize.X, 100) };
		_winOverlay.AddChild(band);
		var lbl = new Label { Text = "You Won!", HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(vpSize.X, 80), Position = new Vector2(0, vpSize.Y / 2 - 40) };
		lbl.AddThemeFontSizeOverride("font_size", 52);
		_winOverlay.AddChild(lbl);
	}

	private void ExitWinState()
	{
		_gameWon = false;
		_winOverlay?.QueueFree();
		_winOverlay = null;
	}

	private static bool IsPointInCard(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

	private static bool IsPointInDropZone(Vector2 point, Vector2 center) =>
		Math.Abs(point.X - center.X) <= Card.CardWidth * 0.8f &&
		Math.Abs(point.Y - center.Y) <= Card.CardHeight * 0.9f;
}
