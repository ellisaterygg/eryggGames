using Godot;
using System;

namespace EryggGames;

public partial class Launcher : Control
{
    private Node _currentGame;
    private Control _menuLayer;
    private PackedScene _freeCellScene;
    private PackedScene _pyramidScene;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _freeCellScene = GD.Load<PackedScene>("res://scenes/freecell/FreeCell.tscn");
        _pyramidScene = GD.Load<PackedScene>("res://scenes/pyramid/Pyramid.tscn");

        SetupUI();
    }

    private void SetupUI()
    {
        _menuLayer = new Control { Name = "MenuLayer" };
        _menuLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_menuLayer);

        var bg = new ColorRect { Color = new Color(0.1f, 0.2f, 0.15f), Name = "Background" };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        _menuLayer.AddChild(bg);

        var title = new Label {
            Text = "Card Games",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(0, 100),
            Size = new Vector2(720, 100)
        };
        title.AddThemeFontSizeOverride("font_size", 64);
        _menuLayer.AddChild(title);

        var vBox = new VBoxContainer {
            Position = new Vector2(210, 300),
            Size = new Vector2(300, 400),
            ThemeTypeVariation = "VBoxContainer"
        };
        _menuLayer.AddChild(vBox);

        vBox.AddChild(MakeGameButton("FreeCell", () => SwitchGame("FreeCell")));
        vBox.AddChild(MakeGameButton("Pyramid", () => SwitchGame("Pyramid")));
    }

    private Button MakeGameButton(string text, Action onPressed)
    {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(0, 80)
        };
        btn.AddThemeFontSizeOverride("font_size", 32);
        btn.Pressed += onPressed;
        return btn;
    }

    public void SwitchGame(string gameName)
    {
        if (_currentGame != null)
        {
            _currentGame.QueueFree();
            _currentGame = null;
        }

        if (gameName == "Launcher")
        {
            _menuLayer.Visible = true;
            return;
        }

        PackedScene scene = gameName switch
        {
            "FreeCell" => _freeCellScene,
            "Pyramid" => _pyramidScene,
            _ => null
        };

        if (scene != null)
        {
            _menuLayer.Visible = false;
            _currentGame = scene.Instantiate();
            AddChild(_currentGame);
        }
        else if (gameName == "Pyramid")
        {
            GD.Print("Pyramid not implemented yet!");
        }
    }
}
