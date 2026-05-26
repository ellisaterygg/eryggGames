using Godot;
using System;
using System.Text.Json.Serialization;

namespace EryggGames.Shared;

public class ScoreState
{
    public long CurrentScore { get; set; } = 1000;
    public bool AllowNegative { get; set; } = false;
}

public static class ScoreManager
{
    private static ScoreState _state = new();
    public static event Action<long>? ScoreChanged;

    public static long CurrentScore => _state.CurrentScore;
    public static bool AllowNegative => _state.AllowNegative;

    static ScoreManager()
    {
        Load();
    }

    public static void AddScore(long amount)
    {
        if (amount == 0) return;
        _state.CurrentScore += amount;
        Save();
        ScoreChanged?.Invoke(_state.CurrentScore);
    }

    public static void SubtractScore(long amount)
    {
        if (amount == 0) return;
        _state.CurrentScore -= amount;
        Save();
        ScoreChanged?.Invoke(_state.CurrentScore);
    }

    public static void SetAllowNegative(bool allow)
    {
        _state.AllowNegative = allow;
        Save();
    }

    public static void ResetScore()
    {
        _state.CurrentScore = 500;
        Save();
        ScoreChanged?.Invoke(_state.CurrentScore);
    }

    private static void Save()
    {
        SaveManager.SaveGame("GlobalScore", _state);
    }

    private static void Load()
    {
        var saved = SaveManager.LoadGame<ScoreState>("GlobalScore");
        if (saved != null)
        {
            _state = saved;
        }
    }
}
