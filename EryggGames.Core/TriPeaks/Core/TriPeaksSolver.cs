#nullable enable
using System;
using System.Collections.Generic;
using EryggGames.Core;

namespace EryggGames.TriPeaks.Core;

public static class TriPeaksSolver
{
    private const int MaxNodes = 100_000;

    public static bool IsWinnable(TriPeaksState initialState)
    {
        var peaks = new CardModel[28];
        int pi = 0;
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < initialState.Peaks[r].Length; c++)
                peaks[pi++] = initialState.Peaks[r][c]!;

        var stock = initialState.Stock.ToArray();
        var wasteTop = initialState.Waste[^1];

        var seen = new HashSet<ulong>();
        int nodes = 0;
        return Solve(0x0FFFFFFFu, stock, stock.Length - 1, wasteTop.Rank, peaks, seen, ref nodes);
    }

    private static bool Solve(
        ulong mask, CardModel[] stock, int sPtr, Rank wRank,
        CardModel[] peaks, HashSet<ulong> seen, ref int nodes)
    {
        if (mask == 0) return true;
        if (++nodes > MaxNodes) return false;

        // Pack state: mask(28) | (sPtr+1)(5, bits 28-32) | wRank(4, bits 33-36)
        ulong key = mask | ((ulong)(sPtr + 1) << 28) | ((ulong)wRank << 33);
        if (!seen.Add(key)) return false;

        Span<int> exp = stackalloc int[28];
        int expN = GetExposed(mask, exp);

        // 1. Move exposed peak card to waste
        for (int i = 0; i < expN; i++)
        {
            var rank = peaks[exp[i]].Rank;
            if (IsAdjacent(rank, wRank))
                if (Solve(mask & ~(1UL << exp[i]), stock, sPtr, rank, peaks, seen, ref nodes)) return true;
        }

        // 2. Draw from stock
        if (sPtr >= 0)
            if (Solve(mask, stock, sPtr - 1, stock[sPtr].Rank, peaks, seen, ref nodes)) return true;

        return false;
    }

    private static bool IsAdjacent(Rank r1, Rank r2)
    {
        int v1 = (int)r1, v2 = (int)r2;
        if (Math.Abs(v1 - v2) == 1) return true;
        return (v1 == 1 && v2 == 13) || (v1 == 13 && v2 == 1);
    }

    // TriPeaks layout (flat indices):
    //   Row 0 (3 peaks): 0, 1, 2
    //   Row 1 (6):       3, 4, 5, 6, 7, 8
    //   Row 2 (9):       9..17
    //   Row 3 (10):      18..27
    //
    // Children of (r, c) in the next row follow the same child-index formula as GetChildrenFlatIndices.
    private static int GetExposed(ulong mask, Span<int> result)
    {
        int count = 0, idx = 0;
        int[] rowLen = { 3, 6, 9, 10 };
        int[] rowStart = { 0, 3, 9, 18 };

        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < rowLen[r]; c++)
            {
                if ((mask & (1UL << idx)) != 0)
                {
                    bool exposed;
                    if (r == 3)
                    {
                        exposed = true;
                    }
                    else
                    {
                        var (l, ri) = GetChildren(r, c, rowStart[r + 1]);
                        exposed = (mask & (1UL << l)) == 0 && (mask & (1UL << ri)) == 0;
                    }
                    if (exposed) result[count++] = idx;
                }
                idx++;
            }
        }
        return count;
    }

    private static (int left, int right) GetChildren(int r, int c, int nextRowStart)
    {
        int l, ri;
        if (r == 2) { l = c; ri = c + 1; }
        else if (r == 1) { int peak = c / 2; l = c + peak; ri = c + peak + 1; }
        else { l = c * 2; ri = c * 2 + 1; } // r == 0
        return (nextRowStart + l, nextRowStart + ri);
    }
}
