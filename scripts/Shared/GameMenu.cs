using Godot;
using System;
using System.Collections.Generic;

namespace EryggGames.Shared;

public class GameOption
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public int SelectedIndex { get; set; }
}

public partial class GameMenu : CanvasLayer
{
    [Signal] public delegate void NewGameRequestedEventHandler();
    [Signal] public delegate void RestartGameRequestedEventHandler();
    [Signal] public delegate void UndoRequestedEventHandler();
    [Signal] public delegate void GamesRequestedEventHandler();
    [Signal] public delegate void OptionsAppliedEventHandler(bool startNewGame);

    private Control _optionsOverlay = null!;
    private VBoxContainer _optionsContainer = null!;
    private List<GameOption> _currentOptions = new();
    private Dictionary<string, List<CheckBox>> _optionRadioGroups = new();

    private Button _undoBtn = null!;

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

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.85f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _optionsOverlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _optionsOverlay.AddChild(center);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(600, 0),
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        margin.AddThemeConstantOverride("margin_left", 30);
        margin.AddThemeConstantOverride("margin_right", 30);
        panel.AddChild(margin);

        var vBox = new VBoxContainer();
        vBox.AddThemeConstantOverride("separation", 20);
        margin.AddChild(vBox);

        var title = new Label { Text = "Game Options", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        vBox.AddChild(title);
        vBox.AddChild(new HSeparator());

        _optionsContainer = new VBoxContainer();
        _optionsContainer.AddThemeConstantOverride("separation", 15);
        vBox.AddChild(_optionsContainer);

        vBox.AddChild(new HSeparator());

        var btnHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnHBox.AddThemeConstantOverride("separation", 20);
        vBox.AddChild(btnHBox);

        var applyNowBtn = new Button { Text = "Apply & New Game", CustomMinimumSize = new Vector2(180, 60) };
        applyNowBtn.AddThemeFontSizeOverride("font_size", 20);
        applyNowBtn.Pressed += () => ApplyOptions(true);
        btnHBox.AddChild(applyNowBtn);

        var applyNextBtn = new Button { Text = "Apply to Next", CustomMinimumSize = new Vector2(180, 60) };
        applyNextBtn.AddThemeFontSizeOverride("font_size", 20);
        applyNextBtn.Pressed += () => ApplyOptions(false);
        btnHBox.AddChild(applyNextBtn);

        var cancelBtn = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(120, 60) };
        cancelBtn.AddThemeFontSizeOverride("font_size", 20);
        cancelBtn.Pressed += HideOptions;
        btnHBox.AddChild(cancelBtn);
    }

    public void SetOptions(List<GameOption> options)
    {
        _currentOptions = options;
        foreach (Node child in _optionsContainer.GetChildren()) child.QueueFree();
        _optionRadioGroups.Clear();

        foreach (var opt in options)
        {
            var optVBox = new VBoxContainer();
            _optionsContainer.AddChild(optVBox);

            var label = new Label { Text = opt.Label };
            label.AddThemeFontSizeOverride("font_size", 24);
            optVBox.AddChild(label);

            var radioHBox = new HBoxContainer();
            radioHBox.AddThemeConstantOverride("separation", 25);
            optVBox.AddChild(radioHBox);

            var checkboxes = new List<CheckBox>();
            var group = new ButtonGroup();

            for (int i = 0; i < opt.Options.Length; i++)
            {
                var cb = new CheckBox 
                { 
                    Text = opt.Options[i],
                    ButtonGroup = group,
                    ButtonPressed = (i == opt.SelectedIndex)
                };
                cb.AddThemeFontSizeOverride("font_size", 22);
                radioHBox.AddChild(cb);
                checkboxes.Add(cb);
            }

            _optionRadioGroups[opt.Id] = checkboxes;
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
            if (_optionRadioGroups.TryGetValue(opt.Id, out var checkboxes))
            {
                for (int i = 0; i < checkboxes.Count; i++)
                {
                    if (checkboxes[i].ButtonPressed)
                    {
                        opt.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        HideOptions();
        EmitSignal(SignalName.OptionsApplied, startNewGame);
    }
}
