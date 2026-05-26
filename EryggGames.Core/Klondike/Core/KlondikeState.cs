using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Klondike.Core;

public class KlondikeState
{
    public List<CardModel>[] Tableau { get; set; } = new List<CardModel>[7];
    public List<CardModel>[] Foundation { get; set; } = new List<CardModel>[4];
    public List<CardModel> Stock { get; set; } = new();
    public List<CardModel> Waste { get; set; } = new();
    
    // Klondike specific: which cards in tableau are face up (usually just the top few)
    // For simplicity, we can store how many cards are face DOWN in each pile.
    public int[] TableauFaceDown { get; set; } = new int[7];

    public bool IsFinished { get; set; }
    public List<CardModel> InitialDeal { get; set; } = new();

    // Settings
    public int DrawCount { get; set; } = 3; // 1 or 3
    public string? BackgroundFile { get; set; }

    public KlondikeState()
    {
        for (int i = 0; i < 7; i++) Tableau[i] = new();
        for (int i = 0; i < 4; i++) Foundation[i] = new();
    }

    public KlondikeState Clone()
    {
        var clone = new KlondikeState
        {
            Stock = Stock.ToList(),
            Waste = Waste.ToList(),
            IsFinished = IsFinished,
            InitialDeal = InitialDeal.ToList(),
            DrawCount = DrawCount,
            BackgroundFile = BackgroundFile
        };
        for (int i = 0; i < 7; i++)
        {
            clone.Tableau[i] = Tableau[i].ToList();
            clone.TableauFaceDown[i] = TableauFaceDown[i];
        }
        for (int i = 0; i < 4; i++)
            clone.Foundation[i] = Foundation[i].ToList();
        return clone;
    }
}
