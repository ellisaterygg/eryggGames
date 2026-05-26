using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Shared;

public abstract partial class BaseGameView : Node2D
{
    protected float _topInset = 0f;
    protected GameMenu _menu = null!;
    protected CanvasLayer? _winOverlay;
    protected bool _gameWon;
    protected string _currentBackgroundFile = "";

    // Common Drag State
    protected readonly List<Card> _dragCards = new();
    protected CardPile? _dragOriginPile;
    protected Vector2[] _dragOffsets = Array.Empty<Vector2>();
    protected Vector2 _dragStartPos;
    protected bool _isDrag;

    public override void _Ready()
    {
        _topInset = GetTopSafeInset();
        
        _menu = new GameMenu { Layer = 10 };
        AddChild(_menu);
        _menu.Setup(_topInset, ShowUndoButton);
        _menu.NewGameRequested += OnNewGameRequested;
        _menu.RestartGameRequested += OnRestartGameRequested;
        _menu.UndoRequested += RequestUndo;
        _menu.GamesRequested += ShowGameSelection;
        _menu.OptionsApplied += OnOptionsApplied;

        LoadBackground();
        SetupGame();
    }

    protected virtual bool ShowUndoButton => true;
    protected virtual bool IsGameInProgress => false;

    protected abstract void SetupGame();
    protected abstract void NewGame();
    protected abstract void RestartGame();
    protected virtual void UndoMove() { }
    protected virtual bool CanUndo => true;
    protected virtual void OnOptionsApplied(bool startNewGame) { }

    protected virtual int EntryCost => 0;
    protected virtual int RestartCost => EntryCost;
    protected virtual int UndoCost => 5;
    protected virtual int WinBonus => 0;
    protected virtual int FoundationReward => 0;

    protected void RewardPoints(long amount) => ScoreManager.AddScore(amount);

    protected virtual void OnCardMovedToFoundation()
    {
        RewardPoints(FoundationReward);
    }

    protected bool CanAfford(int cost)
    {
        if (ScoreManager.CurrentScore >= cost || ScoreManager.AllowNegative) return true;
        _menu.ShowBankruptcy(cost, ScoreManager.ResetScore, () => ScoreManager.SetAllowNegative(true));
        return false;
    }

    private void OnNewGameRequested()
    {
        if (!CanAfford(EntryCost)) return;

        if (IsGameInProgress)
        {
            _menu.ShowConfirmation("Start a new game? Current progress will be lost.", () => {
                ScoreManager.SubtractScore(EntryCost);
                NewGame();
            });
        }
        else
        {
            ScoreManager.SubtractScore(EntryCost);
            NewGame();
        }
    }

    private void OnRestartGameRequested()
    {
        if (!CanAfford(RestartCost)) return;

        if (IsGameInProgress)
        {
            _menu.ShowConfirmation("Restart this game? Current progress will be lost.", () => {
                ScoreManager.SubtractScore(RestartCost);
                RestartGame();
            });
        }
        else
        {
            ScoreManager.SubtractScore(RestartCost);
            RestartGame();
        }
    }

    protected virtual void RequestUndo()
    {
        if (!CanUndo) return;

        if (ScoreManager.CurrentScore < UndoCost && !ScoreManager.AllowNegative)
        {
            _menu.ShowBankruptcy(UndoCost, ScoreManager.ResetScore, () => ScoreManager.SetAllowNegative(true));
            return;
        }

        ScoreManager.SubtractScore(UndoCost);
        UndoMove();
    }

    protected void ShowGameSelection()
    {
        var launcher = GetParent<Launcher>();
        launcher?.SwitchGame("Launcher");
    }

    protected string LoadBackground(string? fileName = null)
    {
        var bg = GetNodeOrNull<Sprite2D>("Background");
        if (bg != null)
        {
            _currentBackgroundFile = BackgroundManager.ApplyBackground(bg, fileName);
            return _currentBackgroundFile;
        }
        return "";
    }

    protected float GetTopSafeInset()
    {
        var screenH = (float)DisplayServer.ScreenGetSize().Y;
        var safeTopPx = (float)DisplayServer.GetDisplaySafeArea().Position.Y;
        if (safeTopPx <= 0f || screenH <= 0f) return 0f;
        return safeTopPx / screenH * GetViewport().GetVisibleRect().Size.Y;
    }

    protected void EnterWinState(string message = "You Won!")
    {
        if (_gameWon) return;
        _gameWon = true;
        _menu.SetGameOver(true);
        RewardPoints(WinBonus);

        _winOverlay = new CanvasLayer { Layer = 5 };
        AddChild(_winOverlay);
        
        var vpSize = GetViewport().GetVisibleRect().Size;
        var band = new ColorRect { 
            Color = new Color(0, 0, 0, 0.45f), 
            Position = new Vector2(0, vpSize.Y / 2 - 50), 
            Size = new Vector2(vpSize.X, 100) 
        };
        _winOverlay.AddChild(band);
        
        var lbl = new Label { 
            Text = message, 
            HorizontalAlignment = HorizontalAlignment.Center, 
            Size = new Vector2(vpSize.X, 80), 
            Position = new Vector2(0, vpSize.Y / 2 - 40) 
        };
        lbl.AddThemeFontSizeOverride("font_size", 52);
        _winOverlay.AddChild(lbl);
    }

    protected void ExitWinState()
    {
        _gameWon = false;
        _menu.SetGameOver(false);
        _winOverlay?.QueueFree();
        _winOverlay = null;
    }

    // ── Common Input Logic ──────────────────────────────────────────────────

    public override void _Input(InputEvent @event)
    {
        if (_gameWon) return;

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
                if (mb.DoubleClick) HandleMouseButtonDoubleClicked(mb.GlobalPosition);
                else HandleMouseButtonPressed(mb.GlobalPosition);
                break;
            case InputEventMouseMotion mm when _dragCards.Count > 0:
                HandleMouseMotion(mm.GlobalPosition);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb:
                if (_dragCards.Count > 0) HandleMouseButtonReleased(mb.GlobalPosition);
                break;
        }
    }

    protected virtual void HandleMouseButtonDoubleClicked(Vector2 globalPos) { }

    protected virtual void HandleMouseButtonPressed(Vector2 globalPos)
    {
        if (_dragCards.Count > 0) return;

        var card = GetCardAt(globalPos);
        if (card == null)
        {
            HandleEmptySpaceClick(globalPos);
            return;
        }

        if (ShouldAllowDrag(card))
        {
            _dragStartPos = globalPos;
            _isDrag = false;
            BeginDrag(card, globalPos);
        }
        else
        {
            HandleCardClick(card);
        }
    }

    protected virtual void HandleMouseMotion(Vector2 globalPos)
    {
        if (!_isDrag && globalPos.DistanceTo(_dragStartPos) > 10f)
        {
            _isDrag = true;
        }

        if (_isDrag)
        {
            for (int i = 0; i < _dragCards.Count; i++)
                _dragCards[i].Position = globalPos + _dragOffsets[i];
        }
    }

    protected virtual void HandleMouseButtonReleased(Vector2 globalPos)
    {
        if (!_isDrag && _dragCards.Count > 0)
        {
            var card = _dragCards[0];
            CancelDrag();
            _dragCards.Clear();
            _dragOriginPile = null;
            HandleCardClick(card);
        }
        else if (_dragCards.Count > 0)
        {
            EndDrag(globalPos);
        }
    }

    protected virtual void BeginDrag(Card card, Vector2 globalMousePos)
    {
        var pile = card.CurrentPile;
        int count = 1;
        int idx = 0;

        if (pile != null)
        {
            idx = pile.Cards.IndexOf(card);
            count = pile.Count - idx;
            if (!CanMoveStack(pile, card, count)) return;
            _dragOriginPile = pile;
        }
        else
        {
            _dragOriginPile = null;
        }

        // IMPORTANT: Snap current state BEFORE we start removing cards from their logical piles
        OnBeforeDragStarted();

        var globalPositions = new Vector2[count];
        List<Card> cards;
        
        if (pile != null)
        {
            for (int i = 0; i < count; i++) globalPositions[i] = pile.Cards[idx + i].GlobalPosition;
            cards = pile.RemoveTopCards(count);
        }
        else
        {
            globalPositions[0] = card.GlobalPosition;
            cards = new List<Card> { card };
            card.GetParent()?.RemoveChild(card);
        }

        _dragOffsets = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            AddChild(cards[i]);
            cards[i].Position = globalPositions[i];
            _dragOffsets[i] = globalPositions[i] - globalMousePos;
            cards[i].ZIndex = 100 + i;
            _dragCards.Add(cards[i]);
        }
    }

    protected virtual void EndDrag(Vector2 globalMousePos)
    {
        var bottomCard = _dragCards[0];
        var target = FindDropTarget(bottomCard);

        bool valid = false;
        if (target != null && target != _dragOriginPile)
        {
            valid = CanDropCards(bottomCard, _dragCards, target);
        }

        if (valid)
        {
            ExecuteDrop(target!, _dragCards);
        }
        else
        {
            CancelDrag();
        }

        _dragCards.Clear();
        _dragOriginPile = null;
        _dragOffsets = Array.Empty<Vector2>();
        
        OnDragEnded(valid, valid ? target : null);
    }

    protected virtual void CancelDrag()
    {
        if (_dragOriginPile != null)
        {
            foreach (var c in _dragCards) _dragOriginPile.AddCard(c);
        }
    }

    // ── Rules and Overrides ──────────────────────────────────────────────────

    protected abstract bool ShouldAllowDrag(Card card);
    protected virtual bool CanMoveStack(CardPile pile, Card card, int count) => count == 1;
    protected abstract CardPile? FindDropTarget(Card draggingCard);
    protected abstract bool CanDropCards(Card bottomCard, List<Card> draggingCards, CardPile target);
    protected abstract void ExecuteDrop(CardPile target, List<Card> draggingCards);
    
    protected virtual void HandleCardClick(Card card) { }
    protected virtual void HandleEmptySpaceClick(Vector2 globalPos) { }
    protected virtual void OnBeforeDragStarted() { }
    protected virtual void OnDragEnded(bool valid, CardPile? target) { }

    // ── Utilities ────────────────────────────────────────────────────────────

    protected virtual Card? GetCardAt(Vector2 globalPos)
    {
        foreach (var p in GetPilesForInput().Reverse())
        {
            for (int i = p.Count - 1; i >= 0; i--)
            {
                if (IsPointInCard(globalPos, p.Cards[i].GlobalPosition)) return p.Cards[i];
            }
        }
        return null;
    }

    protected abstract IEnumerable<CardPile> GetPilesForInput();

    protected bool IsPointInCard(Vector2 point, Vector2 center) =>
        Math.Abs(point.X - center.X) <= Card.CardWidth / 2 &&
        Math.Abs(point.Y - center.Y) <= Card.CardHeight / 2;

    protected bool IsPointInPile(Vector2 globalPos, CardPile pile) =>
        pile.GetGlobalRect().HasPoint(globalPos);

    protected bool IsPointInNode(Vector2 globalPos, Node2D node)
    {
        var rect = new Rect2(node.GlobalPosition - new Vector2(Card.CardWidth / 2, Card.CardHeight / 2), Card.CardWidth, Card.CardHeight);
        return rect.HasPoint(globalPos);
    }
}
