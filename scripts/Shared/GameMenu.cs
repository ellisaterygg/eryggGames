using Godot;
using System;
using System.Collections.Generic;

namespace EryggGames.Shared;

public class GameOption
{
    public string Id { get; set; }
    public string Label { get; set; }
    public string[] Options { get; set; }
    public int SelectedIndex { get; set; }
}

public partial class GameMenu : CanvasLayer
{
    [Signal] public delegate void NewGameRequestedEventHandler();
    [Signal] public delegate void RestartGameRequestedEventHandler();
    [Signal] public delegate void UndoRequestedEventHandler();
    [Signal] public delegate void GamesRequestedEventHandler();
    [Signal] public delegate void OptionsAppliedEventHandler(bool startNewGame);

    private Control _optionsOverlay;
    private VBoxContainer _optionsContainer;
    private List<GameOption> _currentOptions = new();
    private Dictionary<string, OptionButton> _optionButtons = new();

    private Button _undoBtn;

    public void Setup(float topInset, bool showUndo = true)
    {
        float barH = 95f + topInset;
        var bar = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.50f),
            Size  = new Vector2(720, barH),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(bar);

        float btnY = topInset + 22f;
        bar.AddChild(MakeMenuButton("New",     new Vector2(25,  btnY), () => EmitSignal(SignalName.NewGameRequested)));
        bar.AddChild(MakeMenuButton("Restart", new Vector2(165, btnY), () => EmitSignal(SignalName.RestartGameRequested)));
        
        _undoBtn = MakeMenuButton("Undo", new Vector2(305, btnY), () => EmitSignal(SignalName.UndoRequested));
        _undoBtn.Visible = showUndo;
        bar.AddChild(_undoBtn);

        bar.AddChild(MakeMenuButton("Options", new Vector2(445, btnY), ShowOptions));
        bar.AddChild(MakeMenuButton("Games",   new Vector2(585, btnY), () => EmitSignal(SignalName.GamesRequested)));

        SetupOptionsOverlay();
    }

    public void SetUndoEnabled(bool enabled)
    {
        if (_undoBtn != null) _undoBtn.Disabled = !enabled;
    }

    private Button MakeMenuButton(string text, Vector2 pos, Action handler)
    {
        var btn = new Button
        {
            Text     = text,
            Position = pos,
            Size     = new Vector2(110, 52),
        };
        btn.AddThemeFontSizeOverride("font_size", 20);
        btn.Pressed += handler;
        return btn;
    }

    private void SetupOptionsOverlay()
    {
        _optionsOverlay = new Control { Name = "OptionsOverlay", Visible = false };
        _optionsOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_optionsOverlay);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _optionsOverlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _optionsOverlay.AddChild(center);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(500, 0),
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        panel.AddChild(margin);

        var vBox = new VBoxContainer();
        margin.AddChild(vBox);

        var title = new Label { Text = "Game Options", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        vBox.AddChild(title);
        vBox.AddChild(new HSeparator());

        _optionsContainer = new VBoxContainer();
        vBox.AddChild(_optionsContainer);

        vBox.AddChild(new HSeparator());

        var btnHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vBox.AddChild(btnHBox);

        var applyNowBtn = new Button { Text = "Apply & New Game" };
        applyNowBtn.Pressed += () => ApplyOptions(true);
        btnHBox.AddChild(applyNowBtn);

        var applyNextBtn = new Button { Text = "Apply to Next" };
        applyNextBtn.Pressed += () => ApplyOptions(false);
        btnHBox.AddChild(applyNextBtn);

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Pressed += HideOptions;
        btnHBox.AddChild(cancelBtn);
    }

    public void SetOptions(List<GameOption> options)
    {
        _currentOptions = options;
        foreach (Node child in _optionsContainer.GetChildren()) child.QueueFree();
        _optionButtons.Clear();

        foreach (var opt in options)
        {
            var hBox = new HBoxContainer();
            _optionsContainer.AddChild(hBox);

            var label = new Label { Text = opt.Label, CustomMinimumSize = new Vector2(150, 0) };
            hBox.AddChild(label);

            var combo = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
            foreach (var o in opt.Options) combo.AddItem(o);
            combo.Selected = opt.SelectedIndex;
            hBox.AddChild(combo);

            _optionButtons[opt.Id] = combo;
        }
    }

    private void ShowOptions()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        _optionsOverlay.Size = vpSize;
        _optionsOverlay.Position = Vector2.Zero;
        _optionsOverlay.Visible = true;
    }

    private void HideOptions()
    {
        _optionsOverlay.Visible = false;
    }

    private void ApplyOptions(bool startNewGame)
    {
        foreach (var opt in _currentOptions)
        {
            if (_optionButtons.TryGetValue(opt.Id, out var combo))
            {
                opt.SelectedIndex = combo.Selected;
            }
        }
        HideOptions();
        EmitSignal(SignalName.OptionsApplied, startNewGame);
    }
}
