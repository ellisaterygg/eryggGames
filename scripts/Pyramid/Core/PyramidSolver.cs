using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Pyramid.Core;

public static class PyramidSolver
{
    private class SolverState
    {
        public ulong PyramidMask; 
        public int StockPtr;      // Index of next card to draw from initial stock
        public ulong WasteMask;   // Bitmask of cards from initial stock currently in waste
        public int Passes;
        
        public override bool Equals(object? obj)
        {
            if (obj is not SolverState other) return false;
            return PyramidMask == other.PyramidMask && 
                   StockPtr == other.StockPtr && 
                   WasteMask == other.WasteMask &&
                   Passes == other.Passes;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PyramidMask, StockPtr, WasteMask, Passes);
        }
    }

    public static bool IsWinnable(PyramidState initialState)
    {
        var pyramid = new List<CardModel>();
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++)
                pyramid.Add(initialState.Pyramid[r][c]!);

        var stock = initialState.Stock.ToList();
        var seen = new HashSet<SolverState>();
        
        return Solve(0xFFFFFFF, stock, stock.Count, 0, 0, pyramid, seen);
    }

    private static bool Solve(ulong pMask, List<CardModel> stock, int sPtr, ulong wMask, int passes, List<CardModel> pyramid, HashSet<SolverState> seen)
    {
        if (pMask == 0) return true;

        var state = new SolverState { PyramidMask = pMask, StockPtr = sPtr, WasteMask = wMask, Passes = passes };
        if (seen.Contains(state)) return false;
        seen.Add(state);

        var exposedIdx = GetExposedIndices(pMask);
        
        // Top of Waste (already drawn)
        int topWasteIdx = -1;
        for (int i = stock.Count - 1; i >= sPtr; i--)
        {
            if ((wMask & (1UL << i)) != 0) { topWasteIdx = i; break; }
        }
        var wasteCard = topWasteIdx >= 0 ? stock[topWasteIdx] : null;

        // Top of Stock (ready to be drawn or matched)
        int topStockIdx = sPtr - 1;
        var stockCard = topStockIdx >= 0 ? stock[topStockIdx] : null;

        // 1. Remove Kings
        foreach (var idx in exposedIdx)
            if (pyramid[idx].Rank == Rank.King)
                if (Solve(pMask & ~(1UL << idx), stock, sPtr, wMask, passes, pyramid, seen)) return true;

        if (wasteCard?.Rank == Rank.King)
            if (Solve(pMask, stock, sPtr, wMask & ~(1UL << topWasteIdx), passes, pyramid, seen)) return true;

        if (stockCard?.Rank == Rank.King)
            if (Solve(pMask, stock, topStockIdx, wMask, passes, pyramid, seen)) return true; // Just remove it from stock

        // 2. Match two from Pyramid
        for (int i = 0; i < exposedIdx.Count; i++)
        {
            for (int j = i + 1; j < exposedIdx.Count; j++)
            {
                if ((int)pyramid[exposedIdx[i]].Rank + (int)pyramid[exposedIdx[j]].Rank == 13)
                    if (Solve(pMask & ~(1UL << exposedIdx[i]) & ~(1UL << exposedIdx[j]), stock, sPtr, wMask, passes, pyramid, seen)) return true;
            }
        }

        // 3. Match Pyramid with Waste
        if (wasteCard != null)
        {
            foreach (var idx in exposedIdx)
                if ((int)pyramid[idx].Rank + (int)wasteCard.Rank == 13)
                    if (Solve(pMask & ~(1UL << idx), stock, sPtr, wMask & ~(1UL << topWasteIdx), passes, pyramid, seen)) return true;
        }

        // 4. Match Pyramid with Stock (NEW)
        if (stockCard != null)
        {
            foreach (var idx in exposedIdx)
                if ((int)pyramid[idx].Rank + (int)stockCard.Rank == 13)
                    if (Solve(pMask & ~(1UL << idx), stock, topStockIdx, wMask, passes, pyramid, seen)) return true;
        }

        // 5. Match Stock with Waste (NEW)
        if (stockCard != null && wasteCard != null)
        {
            if ((int)stockCard.Rank + (int)wasteCard.Rank == 13)
                if (Solve(pMask, stock, topStockIdx, wMask & ~(1UL << topWasteIdx), passes, pyramid, seen)) return true;
        }

        // 6. Draw (moves stockCard to waste)
        if (sPtr > 0)
        {
            if (Solve(pMask, stock, sPtr - 1, wMask | (1UL << (sPtr - 1)), passes, pyramid, seen)) return true;
        }
        else if (passes < 2)
        {
            // Recycle waste back to stock
            if (wMask != 0)
                if (Solve(pMask, stock, stock.Count, 0, passes + 1, pyramid, seen)) return true;
        }

        return false;
    }

    private static List<int> GetExposedIndices(ulong mask)
    {
        var exposed = new List<int>();
        int idx = 0;
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                if ((mask & (1UL << idx)) != 0)
                {
                    if (r == 6) exposed.Add(idx);
                    else
                    {
                        int leftChild = GetFlatIdx(r + 1, c);
                        int rightChild = GetFlatIdx(r + 1, c + 1);
                        if ((mask & (1UL << leftChild)) == 0 && (mask & (1UL << rightChild)) == 0)
                            exposed.Add(idx);
                    }
                }
                idx++;
            }
        }
        return exposed;
    }

    private static int GetFlatIdx(int r, int c)
    {
        int idx = 0;
        for (int i = 0; i < r; i++) idx += (i + 1);
        return idx + c;
    }
}
