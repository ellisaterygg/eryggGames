#nullable enable
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.TriPeaks.Core;

public class TriPeaksState
{
    public List<CardModel> InitialDeal { get; set; } = new List<CardModel>();
    public List<CardModel> Stock { get; set; } = new List<CardModel>();
    public List<CardModel> Waste { get; set; } = new List<CardModel>();
    
    // Rows: 0 (3 cards), 1 (6 cards), 2 (9 cards), 3 (10 cards)
    public CardModel?[][] Peaks { get; set; } = new CardModel?[4][];

    public bool IsFinished { get; set; }
    public bool WinnableOnly { get; set; } = true;
    public string? BackgroundFile { get; set; }

    public TriPeaksState()
    {
        Peaks[0] = new CardModel?[3];
        Peaks[1] = new CardModel?[6];
        Peaks[2] = new CardModel?[9];
        Peaks[3] = new CardModel?[10];
    }

    public TriPeaksState Clone()
    {
        var clone = new TriPeaksState();
        clone.InitialDeal = InitialDeal.ToList();
        clone.Stock = Stock.ToList();
        clone.Waste = Waste.ToList();
        clone.IsFinished = IsFinished;
        clone.WinnableOnly = WinnableOnly;
        clone.BackgroundFile = BackgroundFile;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < Peaks[i].Length; j++)
                clone.Peaks[i][j] = Peaks[i][j];
        }
        return clone;
    }
}
