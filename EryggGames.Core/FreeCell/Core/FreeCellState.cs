using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.FreeCell.Core;

public record MoveResult(bool IsValid, string Reason = "");

public class FreeCellState
{
    public List<CardModel>[] Tableau { get; set; } = new List<CardModel>[8];
    public List<CardModel>[] FreeCells { get; set; } = new List<CardModel>[4];
    public List<CardModel>[] Foundations { get; set; } = new List<CardModel>[4];
    public Suit?[] FoundationSuits { get; set; } = new Suit?[4];
    public bool IsFinished { get; set; }
    public string? BackgroundFile { get; set; }

    public FreeCellState()
    {
        for (int i = 0; i < 8; i++) Tableau[i] = new List<CardModel>();
        for (int i = 0; i < 4; i++) FreeCells[i] = new List<CardModel>();
        for (int i = 0; i < 4; i++) Foundations[i] = new List<CardModel>();
    }

    public FreeCellState Clone()
    {
        var clone = new FreeCellState();
        for (int i = 0; i < 8; i++) clone.Tableau[i] = Tableau[i].ToList();
        for (int i = 0; i < 4; i++) clone.FreeCells[i] = FreeCells[i].ToList();
        for (int i = 0; i < 4; i++) clone.Foundations[i] = Foundations[i].ToList();
        for (int i = 0; i < 4; i++) clone.FoundationSuits[i] = FoundationSuits[i];
        clone.IsFinished = IsFinished;
        clone.BackgroundFile = BackgroundFile;
        return clone;
    }
}
