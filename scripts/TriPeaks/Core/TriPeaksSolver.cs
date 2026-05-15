using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.TriPeaks.Core;

public static class TriPeaksSolver
{
    private class SolverState
    {
        public ulong PeaksMask; // 28 bits
        public int StockIdx;    // Index of current card in waste
        public Rank WasteRank;

        public override bool Equals(object? obj)
        {
            if (obj is not SolverState other) return false;
            return PeaksMask == other.PeaksMask && StockIdx == other.StockIdx && WasteRank == other.WasteRank;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PeaksMask, StockIdx, WasteRank);
        }
    }

    public static bool IsWinnable(TriPeaksState initialState)
    {
        var peaks = new List<CardModel>();
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < initialState.Peaks[r].Length; c++)
                peaks.Add(initialState.Peaks[r][c]!);

        var stock = initialState.Stock.ToList();
        var seen = new HashSet<SolverState>();
        
        // waste initial
        var wasteTop = initialState.Waste[^1];

        return Solve(0xFFFFFFF, stock, stock.Count - 1, wasteTop.Rank, peaks, seen);
    }

    private static bool Solve(ulong mask, List<CardModel> stock, int sPtr, Rank wRank, List<CardModel> peaks, HashSet<SolverState> seen)
    {
        if (mask == 0) return true;

        var state = new SolverState { PeaksMask = mask, StockIdx = sPtr, WasteRank = wRank };
        if (seen.Contains(state)) return false;
        seen.Add(state);

        var exposedIdx = GetExposedIndices(mask);

        // 1. Move from Peaks to Waste
        foreach (var idx in exposedIdx)
        {
            if (IsValidMove(peaks[idx].Rank, wRank))
            {
                if (Solve(mask & ~(1UL << idx), stock, sPtr, peaks[idx].Rank, peaks, seen)) return true;
            }
        }

        // 2. Draw from Stock
        if (sPtr >= 0)
        {
            if (Solve(mask, stock, sPtr - 1, stock[sPtr].Rank, peaks, seen)) return true;
        }

        return false;
    }

    private static bool IsValidMove(Rank r1, Rank r2)
    {
        int v1 = (int)r1;
        int v2 = (int)r2;
        if (Math.Abs(v1 - v2) == 1) return true;
        if ((v1 == 1 && v2 == 13) || (v1 == 13 && v2 == 1)) return true;
        return false;
    }

    private static List<int> GetExposedIndices(ulong mask)
    {
        var exposed = new List<int>();
        int idx = 0;
        for (int r = 0; r < 4; r++)
        {
            int rowLen = GetRowLength(r);
            for (int c = 0; c < rowLen; c++)
            {
                if ((mask & (1UL << idx)) != 0)
                {
                    if (r == 3) exposed.Add(idx);
                    else
                    {
                        var (l, rIdx) = GetChildrenFlatIndices(r, c);
                        if ((mask & (1UL << l)) == 0 && (mask & (1UL << rIdx)) == 0)
                            exposed.Add(idx);
                    }
                }
                idx++;
            }
        }
        return exposed;
    }

    private static int GetRowLength(int r) => r switch { 0 => 3, 1 => 6, 2 => 9, 3 => 10, _ => 0 };

    private static (int left, int right) GetChildrenFlatIndices(int r, int c)
    {
        int nextRowStart = 0;
        for (int i = 0; i <= r; i++) nextRowStart += GetRowLength(i);

        int l, ri;
        if (r == 2)
        {
            l = c;
            ri = c + 1;
        }
        else if (r == 1)
        {
            int peak = c / 2;
            l = c + peak;
            ri = c + peak + 1;
        }
        else // r == 0
        {
            l = c * 2;
            ri = c * 2 + 1;
        }

        return (nextRowStart + l, nextRowStart + ri);
    }
}
