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

    private Control _confirmOverlay = null!;
    private Label _confirmLabel = null!;
    private Action? _onConfirmAction;

    private Control _bankruptOverlay = null!;
    private Label _bankruptLabel = null!;
    private Action? _onBankruptReset;
    private Action? _onBankruptNegative;

    private Button _applyNextBtn = null!;
    private bool _isGameOver;

    private Button _undoBtn = null!;
    private Label _scoreLabel = null!;
    private Action<long>? _scoreHandler;

    public override void _ExitTree()
    {
        if (_scoreHandler != null) ScoreManager.ScoreChanged -= _scoreHandler;
    }

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

        _scoreLabel = new Label
        {
            Text = $"Points: {ScoreManager.CurrentScore}",
            Position = new Vector2(25, topInset + 5),
            Size = new Vector2(200, 20),
        };
        _scoreLabel.AddThemeFontSizeOverride("font_size", 18);
        _scoreLabel.AddThemeColorOverride("font_color", new Color(0.8f, 1f, 0.8f));
        bar.AddChild(_scoreLabel);

        _scoreHandler = (score) => _scoreLabel.Text = $"Points: {score}";
        ScoreManager.ScoreChanged += _scoreHandler;

        float btnY = topInset + 30f;
        bar.AddChild(MakeMenuButton("New",     new Vector2(25,  btnY), () => EmitSignal(SignalName.NewGameRequested)));
        bar.AddChild(MakeMenuButton("Restart", new Vector2(165, btnY), () => EmitSignal(SignalName.RestartGameRequested)));
        
        _undoBtn = MakeMenuButton("Undo", new Vector2(305, btnY), () => EmitSignal(SignalName.UndoRequested));
        _undoBtn.Visible = showUndo;
        bar.AddChild(_undoBtn);

        bar.AddChild(MakeMenuButton("Options", new Vector2(445, btnY), ShowOptions));
        bar.AddChild(MakeMenuButton("Games",   new Vector2(585, btnY), () => EmitSignal(SignalName.GamesRequested)));

        SetupOptionsOverlay();
        SetupConfirmOverlay();
        SetupBankruptOverlay();
    }

    public void SetGameOver(bool isOver)
    {
        _isGameOver = isOver;
    }

    public void ShowConfirmation(string message, Action onConfirm)
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        _confirmOverlay.Size = vpSize;
        _confirmOverlay.Position = Vector2.Zero;
        _confirmLabel.Text = message;
        _onConfirmAction = onConfirm;
        _confirmOverlay.Visible = true;
    }

    public void ShowBankruptcy(int required, Action onReset, Action onAllowNegative)
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        _bankruptOverlay.Size = vpSize;
        _bankruptOverlay.Position = Vector2.Zero;
        _bankruptLabel.Text = $"Insufficient points! (Need {required})\n\nChoose how to proceed:";
        _onBankruptReset = onReset;
        _onBankruptNegative = onAllowNegative;
        _bankruptOverlay.Visible = true;
    }

    private void SetupBankruptOverlay()
    {
        _bankruptOverlay = new Control { Name = "BankruptOverlay", Visible = false };
        _bankruptOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_bankruptOverlay);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.85f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bankruptOverlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bankruptOverlay.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(550, 350) };
        center.AddChild(panel);

        var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vBox.AddThemeConstantOverride("separation", 40);
        panel.AddChild(vBox);

        _bankruptLabel = new Label { 
            Text = "Insufficient points!", 
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _bankruptLabel.AddThemeFontSizeOverride("font_size", 28);
        vBox.AddChild(_bankruptLabel);

        var btnVBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnVBox.AddThemeConstantOverride("separation", 20);
        vBox.AddChild(btnVBox);

        var resetBtn = new Button { Text = "Reset to 500 Points", CustomMinimumSize = new Vector2(300, 70) };
        resetBtn.AddThemeFontSizeOverride("font_size", 24);
        resetBtn.Pressed += () => {
            _bankruptOverlay.Visible = false;
            _onBankruptReset?.Invoke();
        };
        btnVBox.AddChild(resetBtn);

        var negativeBtn = new Button { Text = "Allow Negative Points", CustomMinimumSize = new Vector2(300, 70) };
        negativeBtn.AddThemeFontSizeOverride("font_size", 24);
        negativeBtn.Pressed += () => {
            _bankruptOverlay.Visible = false;
            _onBankruptNegative?.Invoke();
        };
        btnVBox.AddChild(negativeBtn);

        var cancelBtn = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(300, 70) };
        cancelBtn.AddThemeFontSizeOverride("font_size", 24);
        cancelBtn.Pressed += () => _bankruptOverlay.Visible = false;
        btnVBox.AddChild(cancelBtn);
    }

    private void SetupConfirmOverlay()
    {
        _confirmOverlay = new Control { Name = "ConfirmOverlay", Visible = false };
        _confirmOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_confirmOverlay);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirmOverlay.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirmOverlay.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(500, 300) };
        center.AddChild(panel);

        var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vBox.AddThemeConstantOverride("separation", 40);
        panel.AddChild(vBox);

        _confirmLabel = new Label { 
            Text = "Are you sure?", 
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _confirmLabel.AddThemeFontSizeOverride("font_size", 32);
        vBox.AddChild(_confirmLabel);

        var btnHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnHBox.AddThemeConstantOverride("separation", 30);
        vBox.AddChild(btnHBox);

        var yesBtn = new Button { Text = "Yes", CustomMinimumSize = new Vector2(150, 70) };
        yesBtn.AddThemeFontSizeOverride("font_size", 24);
        yesBtn.Pressed += () => {
            _confirmOverlay.Visible = false;
            _onConfirmAction?.Invoke();
        };
        btnHBox.AddChild(yesBtn);

        var noBtn = new Button { Text = "No", CustomMinimumSize = new Vector2(150, 70) };
        noBtn.AddThemeFontSizeOverride("font_size", 24);
        noBtn.Pressed += () => _confirmOverlay.Visible = false;
        btnHBox.AddChild(noBtn);
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

        _applyNextBtn = new Button { Text = "Apply to Next", CustomMinimumSize = new Vector2(180, 60) };
        _applyNextBtn.AddThemeFontSizeOverride("font_size", 20);
        _applyNextBtn.Pressed += () => ApplyOptions(false);
        btnHBox.AddChild(_applyNextBtn);

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
        _applyNextBtn.Visible = !_isGameOver;
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
