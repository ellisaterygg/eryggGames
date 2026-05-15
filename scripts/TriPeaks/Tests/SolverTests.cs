using Godot;
using System;
using System.Collections.Generic;
using EryggGames.Core;
using EryggGames.TriPeaks.Core;

namespace EryggGames.TriPeaks.Tests;

public static class SolverTests
{
    public static void RunTests()
    {
        try
        {
            TestSimpleWinnable();
            TestImpossibleDeals();
            GD.Print("✅ All TriPeaksSolver tests passed!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"❌ TriPeaksSolver Test Failed: {ex.Message}");
        }
    }

    private static void TestSimpleWinnable()
    {
        var state = new TriPeaksState();
        // Base row (row 3) all Aces
        for (int c = 0; c < 10; c++) state.Peaks[3][c] = new CardModel(Suit.Clubs, Rank.Ace);
        // Rows 0-2 all Kings
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < state.Peaks[r].Length; c++)
                state.Peaks[r][c] = new CardModel(Suit.Clubs, Rank.King);
        
        // Initial waste is King (can match Ace)
        state.Waste.Add(new CardModel(Suit.Hearts, Rank.King));

        // Add 10 more Kings to stock so we can definitely clear all Aces if needed
        for(int i=0; i<10; i++) state.Stock.Add(new CardModel(Suit.Spades, Rank.King));
        
        Assert(TriPeaksSolver.IsWinnable(state), "TriPeaks simple winnable should pass");
    }

    private static void TestImpossibleDeals()
    {
        var state = new TriPeaksState();
        // Peaks are all 5s, Waste is Ace, Stock is empty
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < state.Peaks[r].Length; c++)
                state.Peaks[r][c] = new CardModel(Suit.Clubs, Rank.Five);
        
        state.Waste.Add(new CardModel(Suit.Hearts, Rank.Ace));
        state.Stock.Clear();

        Assert(!TriPeaksSolver.IsWinnable(state), "Impossible TriPeaks should fail");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("Test Failed: " + message);
    }
}
