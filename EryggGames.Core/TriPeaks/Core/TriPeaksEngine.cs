using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.TriPeaks.Core;

public static class TriPeaksEngine
{
    public static bool IsExposed(int row, int col, TriPeaksState state)
    {
        if (state.Peaks[row][col] == null) return false;
        if (row == 3) return true;

        // Covering logic
        int leftChild, rightChild;
        if (row == 2)
        {
            leftChild = col;
            rightChild = col + 1;
        }
        else if (row == 1)
        {
            int pyramid = col / 2;
            leftChild = col + pyramid;
            rightChild = col + pyramid + 1;
        }
        else // row == 0
        {
            leftChild = col * 2;
            rightChild = col * 2 + 1;
        }

        return state.Peaks[row + 1][leftChild] == null && state.Peaks[row + 1][rightChild] == null;
    }

    public static bool IsValidMove(CardModel card, CardModel wasteTop)
    {
        if (wasteTop == null) return true;

        int r1 = (int)card.Rank;
        int r2 = (int)wasteTop.Rank;

        // Adjacent ranks
        if (Math.Abs(r1 - r2) == 1) return true;

        // Ace-King wrap around
        if ((r1 == (int)Rank.Ace && r2 == (int)Rank.King) || (r1 == (int)Rank.King && r2 == (int)Rank.Ace))
            return true;

        return false;
    }

    public static bool IsWon(TriPeaksState state)
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < state.Peaks[i].Length; j++)
            {
                if (state.Peaks[i][j] != null) return false;
            }
        }
        return true;
    }
}
