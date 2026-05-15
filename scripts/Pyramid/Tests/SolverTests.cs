using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;
using EryggGames.Pyramid.Core;

namespace EryggGames.Pyramid.Tests;

public static class SolverTests
{
    public static void RunTests()
    {
        try
        {
            TestAllKings();
            TestImpossibleDeals();
            TestSimpleWinnable();
            GD.Print("✅ All PyramidSolver tests passed!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"❌ PyramidSolver Test Failed: {ex.Message}");
        }
    }

    private static void TestAllKings()
    {
        var state = new PyramidState();
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.King);
        }
        // Even with no stock, this should be winnable
        Assert(PyramidSolver.IsWinnable(state), "All Kings should be winnable");
    }

    private static void TestImpossibleDeals()
    {
        var state = new PyramidState();
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.Two);
        }
        // 28 cards of rank 2. Max value is 4. No 13 possible.
        Assert(!PyramidSolver.IsWinnable(state), "Pyramid of all 2s should be impossible");
    }

    private static void TestSimpleWinnable()
    {
        var state = new PyramidState();
        // Construct a pyramid where every card matches its neighbor or stock
        // Row 6: 7 cards. Let's make them all Kings.
        for (int c = 0; c < 7; c++) state.Pyramid[6][c] = new CardModel(Suit.Clubs, Rank.King);
        // Row 5: 6 cards. All Kings.
        for (int c = 0; c < 6; c++) state.Pyramid[5][c] = new CardModel(Suit.Clubs, Rank.King);
        // ... and so on.
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.King);
        }
        
        Assert(PyramidSolver.IsWinnable(state), "Constructed King pyramid winnable");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("Test Failed: " + message);
    }
}
