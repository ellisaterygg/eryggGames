using Godot;
using System;
using System.IO;
using System.Text.Json;

namespace EryggGames.Shared;

public static class SaveManager
{
    private const string SavePathPrefix = "user://save_";

    public static void SaveGame<T>(string gameName, T state)
    {
        string path = ProjectSettings.GlobalizePath($"{SavePathPrefix}{gameName}.json");
        try
        {
            string json = JsonSerializer.Serialize(state);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to save {gameName}: {ex.Message}");
        }
    }

    public static T? LoadGame<T>(string gameName) where T : class
    {
        string path = ProjectSettings.GlobalizePath($"{SavePathPrefix}{gameName}.json");
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to load {gameName}: {ex.Message}");
            return null;
        }
    }
}
