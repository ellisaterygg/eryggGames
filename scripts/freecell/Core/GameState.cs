using System.Collections.Generic;
using System.Linq;

namespace EryggGames.FreeCell.Core;

public record MoveResult(bool IsValid, string Reason = "");

public class GameState
{
    public List<CardModel>[] Tableau { get; } = new List<CardModel>[8];
    public List<CardModel>[] FreeCells { get; } = new List<CardModel>[4];
    public List<CardModel>[] Foundations { get; } = new List<CardModel>[4];
    public Suit?[] FoundationSuits { get; } = new Suit?[4];

    public GameState()
    {
        for (int i = 0; i < 8; i++) Tableau[i] = new List<CardModel>();
        for (int i = 0; i < 4; i++) FreeCells[i] = new List<CardModel>();
        for (int i = 0; i < 4; i++) Foundations[i] = new List<CardModel>();
    }

    public GameState Clone()
    {
        var clone = new GameState();
        for (int i = 0; i < 8; i++) clone.Tableau[i] = Tableau[i].ToList();
        for (int i = 0; i < 4; i++) clone.FreeCells[i] = FreeCells[i].ToList();
        for (int i = 0; i < 4; i++) clone.Foundations[i] = Foundations[i].ToList();
        for (int i = 0; i < 4; i++) clone.FoundationSuits[i] = FoundationSuits[i];
        return clone;
    }
}
