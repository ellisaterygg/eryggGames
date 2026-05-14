using Godot;
using System;
using System.Collections.Generic;
using EryggGames.Shared;

namespace EryggGames.Shared;

public abstract partial class BaseGameView : Node2D
{
    protected float _topInset = 0f;
    protected GameMenu _menu;
    protected CanvasLayer _winOverlay;
    protected bool _gameWon;

    public override void _Ready()
    {
        _topInset = GetTopSafeInset();
        
        _menu = new GameMenu();
        AddChild(_menu);
        _menu.Setup(_topInset, ShowUndoButton);
        _menu.NewGameRequested += NewGame;
        _menu.RestartGameRequested += RestartGame;
        _menu.UndoRequested += UndoMove;
        _menu.GamesRequested += ShowGameSelection;
        _menu.OptionsApplied += OnOptionsApplied;

        LoadBackground();
        SetupGame();
    }

    protected virtual bool ShowUndoButton => true;

    protected abstract void SetupGame();
    protected abstract void NewGame();
    protected abstract void RestartGame();
    protected virtual void UndoMove() { }
    protected virtual void OnOptionsApplied(bool startNewGame) { }

    protected void ShowGameSelection()
    {
        var launcher = GetParent<Launcher>();
        if (launcher != null)
        {
            launcher.SwitchGame("Launcher");
        }
    }

    protected void LoadBackground()
    {
        var bg = GetNodeOrNull<Sprite2D>("Background");
        if (bg != null)
        {
            BackgroundManager.LoadRandomBackground(bg);
        }
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
        _gameWon = true;
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
        _winOverlay?.QueueFree();
        _winOverlay = null;
    }
}
