using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Pyramid.Core;

public static class PyramidEngine
{
    public static int GetValue(Rank rank) => (int)rank;

    public static bool IsExposed(int row, int col, PyramidState state)
    {
        if (row == 6) return state.Pyramid[row][col] != null;
        
        // A card is exposed if both cards below it are gone
        bool leftChildGone = state.Pyramid[row + 1][col] == null;
        bool rightChildGone = state.Pyramid[row + 1][col + 1] == null;
        
        return state.Pyramid[row][col] != null && leftChildGone && rightChildGone;
    }

    public static bool IsValidPair(CardModel a, CardModel b)
    {
        if (a == null) return b.Rank == Rank.King;
        if (b == null) return a.Rank == Rank.King;
        return GetValue(a.Rank) + GetValue(b.Rank) == 13;
    }

    public static bool IsWon(PyramidState state)
    {
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                if (state.Pyramid[i][j] != null) return false;
            }
        }
        return true;
    }
}
