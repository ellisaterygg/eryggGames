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
    private CardPile _stockPile;
    private CardPile _wastePile;
    private readonly CardPile[] _tableau = new CardPile[7];
    private readonly CardPile[] _foundations = new CardPile[4];

    private KlondikeState _state = new();
    private readonly Stack<KlondikeState> _undoStack = new();
    private Dictionary<(Suit, Rank), Card> _cardLookup = new();
    private PackedScene _cardScene;

    private List<GameOption> _options;

    // Drag state
    private readonly List<Card> _dragCards = new();
    private CardPile _dragOriginPile;
    private Vector2[] _dragOffsets;

    protected override void SetupGame()
    {
        _cardScene = GD.Load<PackedScene>("res://scenes/Shared/Card.tscn");
        
        var safeOffset = new Vector2(0, _topInset);

        _stockPile = GetNode<CardPile>("Stock");
        _stockPile.Position += safeOffset;
        _stockPile.PileType = PileType.FreeCell; 
        
        _wastePile = GetNode<CardPile>("Waste");
        _wastePile.Position += safeOffset;
        _wastePile.PileType = PileType.FreeCell;

        for (int i = 0; i < 4; i++)
        {
            _foundations[i] = GetNode<CardPile>($"Foundations/Foundation{i}");
            _foundations[i].Position += safeOffset;
            _foundations[i].PileType = PileType.Foundation;
        }

        for (int i = 0; i < 7; i++)
        {
            _tableau[i] = GetNode<CardPile>($"Tableau/Column{i}");
            _tableau[i].Position += safeOffset;
            _tableau[i].PileType = PileType.Tableau;
        }

        CreateAllCards();
// ... (rest of the method unchanged, but I'll provide the full block in a moment)

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
        foreach (var p in GetAllPiles()) while (!p.IsEmpty) p.RemoveTopCard();

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

    private IEnumerable<CardPile> GetAllPiles() => 
        new[] { _stockPile, _wastePile }.Concat(_foundations).Concat(_tableau);

    protected override void OnOptionsApplied(bool startNewGame)
    {
        var drawOpt = _options.First(o => o.Id == "draw_count");
        _state.DrawCount = drawOpt.SelectedIndex == 0 ? 1 : 3;

        if (startNewGame)
        {
            NewGame();
        }
        else
        {
            SaveGame();
        }
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
        foreach (var p in GetAllPiles()) while (!p.IsEmpty) p.RemoveTopCard();

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

        _menu.SetUndoEnabled(_undoStack.Count > 0);
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
            if (IsPointInPile(pos, _stockPile)) DrawFromStock();
            return;
        }

        if (card.CurrentPile == _stockPile)
        {
            DrawFromStock();
            return;
        }

        if (!card.IsFaceUp) return;

        var pile = card.CurrentPile;
        int idx = pile.Cards.IndexOf(card);
        int count = pile.Count - idx;

        // In Klondike, you can only move a stack if it's in the tableau
        if (pile.PileType != PileType.Tableau && count > 1) return;

        _dragOriginPile = pile;
        var globalPos = new Vector2[count];
        for (int i = 0; i < count; i++) globalPos[i] = pile.Cards[idx + i].GlobalPosition;

        var cards = pile.RemoveTopCards(count);
        _dragOffsets = new Vector2[count];
        _undoStack.Push(CaptureState());

        for (int i = 0; i < count; i++)
        {
            AddChild(cards[i]);
            cards[i].Position = globalPos[i];
            _dragOffsets[i] = globalPos[i] - pos;
            cards[i].ZIndex = 100 + i;
            _dragCards.Add(cards[i]);
        }
    }

    private void UpdateDrag(Vector2 pos)
    {
        for (int i = 0; i < _dragCards.Count; i++)
            _dragCards[i].Position = pos + _dragOffsets[i];
    }

    private void EndDrag(Vector2 pos)
    {
        var bottomCard = _dragCards[0];
        var allPiles = _foundations.Concat(_tableau).ToList();
        var target = OverlapUtils.GetMostOverlapping(bottomCard.GetGlobalRect(), allPiles, p => p.GetGlobalRect());

        bool valid = false;
        if (target != null && target != _dragOriginPile)
        {
            var movingModels = _dragCards.Select(c => new CardModel(c.Suit, c.Rank)).ToList();
            int targetIdx = -1;
            if (target.PileType == PileType.Tableau) targetIdx = Array.IndexOf(_tableau, target);
            else if (target.PileType == PileType.Foundation) targetIdx = Array.IndexOf(_foundations, target);

            valid = KlondikeEngine.CanMove(_state, movingModels, target.PileType, targetIdx);
        }

        var destination = valid ? target : _dragOriginPile;
        foreach (var c in _dragCards) destination.AddCard(c);
        
        if (!valid) _undoStack.Pop(); // Remove the snapshot we took

        _dragCards.Clear();
        _dragOriginPile = null;

        if (valid)
        {
            CheckTableauFlip();
            UpdateStateFromPiles();
            SaveGame();
            if (KlondikeEngine.IsWon(_state)) EnterWinState();
        }
    }

    private void CheckTableauFlip()
    {
        foreach (var p in _tableau)
        {
            if (!p.IsEmpty && !p.TopCard.IsFaceUp)
            {
                p.TopCard.IsFaceUp = true;
            }
        }
    }

    private void DrawFromStock()
    {
        _undoStack.Push(CaptureState());
        if (_stockPile.IsEmpty)
        {
            while (!_wastePile.IsEmpty)
            {
                var card = _wastePile.RemoveTopCard();
                card.IsFaceUp = false;
                _stockPile.AddCard(card);
            }
        }
        else
        {
            int draw = Math.Min(_state.DrawCount, _stockPile.Count);
            for (int i = 0; i < draw; i++)
            {
                var card = _stockPile.RemoveTopCard();
                card.IsFaceUp = true;
                _wastePile.AddCard(card);
            }
        }
        UpdateStateFromPiles();
        SaveGame();
    }

    private Card GetCardAt(Vector2 pos)
    {
        foreach (var p in GetAllPiles().Reverse())
        {
            for (int i = p.Count - 1; i >= 0; i--)
            {
                if (IsPointInCard(pos, p.Cards[i].GlobalPosition)) return p.Cards[i];
            }
        }
        return null;
    }

    private bool IsPointInPile(Vector2 pos, CardPile pile)
    {
        var rect = pile.GetGlobalRect();
        return rect.HasPoint(pos);
    }

    private bool IsPointInCard(Vector2 point, Vector2 center) =>
        Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
        Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

    private KlondikeState CaptureState()
    {
        var snap = new KlondikeState { DrawCount = _state.DrawCount, IsFinished = _gameWon, InitialDeal = _state.InitialDeal };
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
