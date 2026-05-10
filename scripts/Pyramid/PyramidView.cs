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
    private PyramidState _state;
    private PackedScene _cardScene;
    private readonly Dictionary<(int r, int c), Card> _pyramidCards = new();
    private readonly List<Card> _stockCards = new();
    private readonly List<Card> _wasteCards = new();

    private Card _selectedCard;
    private float _topInset;

    public override void _Ready()
    {
        _cardScene = GD.Load<PackedScene>("res://scenes/freecell/Card.tscn");
        _topInset = GetTopSafeInset();
        
        BackgroundManager.LoadRandomBackground(GetNode<Sprite2D>("Background"));
        SetupMenu();
        
        var saved = SaveManager.LoadGame<PyramidState>("Pyramid");
        if (saved != null)
            ApplyState(saved);
        else
            NewGame();
    }

    private float GetTopSafeInset()
    {
        var screenH = (float)DisplayServer.ScreenGetSize().Y;
        var safeTopPx = (float)DisplayServer.GetDisplaySafeArea().Position.Y;
        if (safeTopPx <= 0f || screenH <= 0f) return 0f;
        return safeTopPx / screenH * GetViewport().GetVisibleRect().Size.Y;
    }

    private void SetupMenu()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        float barH = 95f + _topInset;
        var bar = new ColorRect { Color = new Color(0, 0, 0, 0.5f), Size = new Vector2(720, barH) };
        layer.AddChild(bar);

        float btnY = _topInset + 22f;
        bar.AddChild(MakeMenuButton("New", new Vector2(30, btnY), NewGame));
        bar.AddChild(MakeMenuButton("Games", new Vector2(540, btnY), ShowGameSelection));
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

    private void NewGame()
    {
        _state = new PyramidState();
        var deck = new Deck().Shuffle();

        int cardIdx = 0;
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                var (suit, rank) = deck[cardIdx++];
                _state.Pyramid[r][c] = new CardModel(suit, rank);
            }
        }

        while (cardIdx < deck.Count)
        {
            var (suit, rank) = deck[cardIdx++];
            _state.Stock.Add(new CardModel(suit, rank));
        }

        ApplyState(_state);
        SaveManager.SaveGame("Pyramid", _state);
    }

    private void ApplyState(PyramidState state)
    {
        _state = state;
        ClearBoard();

        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                var model = _state.Pyramid[r][c];
                if (model == null) continue;

                var card = _cardScene.Instantiate<Card>();
                card.Init(model.Suit, model.Rank);
                GetNode("PyramidContainer").AddChild(card);
                card.Position = GetPyramidPosition(r, c);
                _pyramidCards[(r, c)] = card;
            }
        }

        foreach (var model in _state.Stock)
        {
            var card = _cardScene.Instantiate<Card>();
            card.Init(model.Suit, model.Rank);
            GetNode("Stock").AddChild(card);
            card.Position = Vector2.Zero; // Stacked
            _stockCards.Add(card);
        }

        foreach (var model in _state.Waste)
        {
            var card = _cardScene.Instantiate<Card>();
            card.Init(model.Suit, model.Rank);
            GetNode("Waste").AddChild(card);
            card.Position = Vector2.Zero; // Stacked
            _wasteCards.Add(card);
        }
        
        UpdateVisuals();
    }

    private void ClearBoard()
    {
        foreach (var c in _pyramidCards.Values) c.QueueFree();
        _pyramidCards.Clear();
        foreach (var c in _stockCards) c.QueueFree();
        _stockCards.Clear();
        foreach (var c in _wasteCards) c.QueueFree();
        _wasteCards.Clear();
        _selectedCard = null;
    }

    private Vector2 GetPyramidPosition(int r, int c)
    {
        float xOffset = (c - r / 2.0f) * (Card.CardWidth + 10);
        float yOffset = r * (Card.CardHeight * 0.4f);
        return new Vector2(xOffset, yOffset);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
        {
            GD.Print($"Pyramid Click at {mb.Position}");
            HandleClick(mb.Position);
        }
    }

    private void HandleClick(Vector2 pos)
    {
        // Check Pyramid
        foreach (var kvp in _pyramidCards)
        {
            if (IsPointInCard(pos, kvp.Value.GlobalPosition))
            {
                if (PyramidEngine.IsExposed(kvp.Key.r, kvp.Key.c, _state))
                {
                    GD.Print($"Clicked Pyramid Card: {kvp.Value.Rank} of {kvp.Value.Suit}");
                    SelectCard(kvp.Value, kvp.Key);
                    return;
                }
            }
        }

        // Check Waste
        if (_wasteCards.Count > 0 && IsPointInCard(pos, _wasteCards[^1].GlobalPosition))
        {
            GD.Print($"Clicked Waste Card: {_wasteCards[^1].Rank}");
            SelectCard(_wasteCards[^1], null);
            return;
        }

        // Check Stock
        if (IsPointInCard(pos, GetNode<Node2D>("Stock").GlobalPosition))
        {
            GD.Print("Clicked Stock");
            DrawFromStock();
            return;
        }
    }

    private void SelectCard(Card card, (int r, int c)? pyramidPos)
    {
        if (card.Rank == Rank.King)
        {
            RemoveCard(card, pyramidPos);
            UpdateGameState();
            return;
        }

        if (_selectedCard == null)
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
            var modelA = new CardModel(_selectedCard.Suit, _selectedCard.Rank);
            var modelB = new CardModel(card.Suit, card.Rank);

            if (PyramidEngine.IsValidPair(modelA, modelB))
            {
                // Find pyramid pos for _selectedCard if it was from pyramid
                (int r, int c)? posA = null;
                foreach(var kvp in _pyramidCards) if(kvp.Value == _selectedCard) { posA = kvp.Key; break; }

                RemoveCard(_selectedCard, posA);
                RemoveCard(card, pyramidPos);
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

    private void RemoveCard(Card card, (int r, int c)? pyramidPos)
    {
        if (pyramidPos.HasValue)
        {
            _state.Pyramid[pyramidPos.Value.r][pyramidPos.Value.c] = null;
            _pyramidCards.Remove(pyramidPos.Value);
        }
        else if (_wasteCards.Contains(card))
        {
            _state.Waste.RemoveAt(_state.Waste.Count - 1);
            _wasteCards.Remove(card);
        }
        card.QueueFree();
    }

    private void DrawFromStock()
    {
        if (_state.Stock.Count > 0)
        {
            var model = _state.Stock[^1];
            _state.Stock.RemoveAt(_state.Stock.Count - 1);
            _state.Waste.Add(model);
            
            var card = _stockCards[^1];
            _stockCards.RemoveAt(_stockCards.Count - 1);
            card.Reparent(GetNode("Waste"));
            card.Position = Vector2.Zero;
            _wasteCards.Add(card);
        }
        else if (_state.DeckPasses < 2) // Total 3 passes (initial + 2 recycles)
        {
            _state.DeckPasses++;
            _state.Stock = _state.Waste.AsEnumerable().Reverse().ToList();
            _state.Waste.Clear();
            ApplyState(_state);
        }
        
        UpdateGameState();
    }

    private void UpdateGameState()
    {
        UpdateVisuals();
        SaveManager.SaveGame("Pyramid", _state);
        if (PyramidEngine.IsWon(_state))
        {
            GD.Print("YOU WON PYRAMID!");
        }
    }

    private void UpdateVisuals()
    {
        // Update Z-indices or labels if needed
    }

    private static bool IsPointInCard(Vector2 point, Vector2 center) =>
        Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
        Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;
}
