#nullable enable
using System;
using System.Collections.Generic;
using EryggGames.Core;

namespace EryggGames.Pyramid.Core;

public static class PyramidSolver
{
    private const int MaxNodes = 200_000;

    public static bool IsWinnable(PyramidState initialState)
    {
        var pyramid = new CardModel[28];
        int pi = 0;
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++)
                pyramid[pi++] = initialState.Pyramid[r][c]!;

        var stock = initialState.Stock.ToArray();
        var seen = new HashSet<ulong>();
        int nodes = 0;
        return Solve(0x0FFFFFFFu, stock, stock.Length, 0UL, 0, pyramid, seen, ref nodes);
    }

    private static bool Solve(
        ulong pMask, CardModel[] stock, int sPtr, ulong wMask, int passes,
        CardModel[] pyramid, HashSet<ulong> seen, ref int nodes)
    {
        if (pMask == 0) return true;
        if (++nodes > MaxNodes) return false;

        // Pack state: pMask(28) | sPtr(5, bits 28-32) | passes(2, bits 33-34) | wMask(24, bits 35-58)
        ulong key = pMask | ((ulong)sPtr << 28) | ((ulong)passes << 33) | (wMask << 35);
        if (!seen.Add(key)) return false;

        // Top of waste: most recently drawn = lowest index >= sPtr with bit set in wMask
        int topWaste = -1;
        for (int i = sPtr; i < stock.Length; i++)
            if ((wMask & (1UL << i)) != 0) { topWaste = i; break; }
        CardModel? wasteCard = topWaste >= 0 ? stock[topWaste] : null;

        int topStock = sPtr - 1;
        CardModel? stockCard = topStock >= 0 ? stock[topStock] : null;

        Span<int> exp = stackalloc int[28];
        int expN = GetExposed(pMask, exp);

        // 1. Remove Kings from pyramid
        for (int i = 0; i < expN; i++)
            if (pyramid[exp[i]].Rank == Rank.King)
                if (Solve(pMask & ~(1UL << exp[i]), stock, sPtr, wMask, passes, pyramid, seen, ref nodes)) return true;

        // 2. Remove King from waste top
        if (wasteCard?.Rank == Rank.King)
            if (Solve(pMask, stock, sPtr, wMask & ~(1UL << topWaste), passes, pyramid, seen, ref nodes)) return true;

        // 3. Remove King from stock top (consumes it: sPtr decrements, wMask unchanged)
        if (stockCard?.Rank == Rank.King)
            if (Solve(pMask, stock, topStock, wMask, passes, pyramid, seen, ref nodes)) return true;

        // 4. Match two exposed pyramid cards
        for (int i = 0; i < expN; i++)
            for (int j = i + 1; j < expN; j++)
                if ((int)pyramid[exp[i]].Rank + (int)pyramid[exp[j]].Rank == 13)
                    if (Solve(pMask & ~(1UL << exp[i]) & ~(1UL << exp[j]), stock, sPtr, wMask, passes, pyramid, seen, ref nodes)) return true;

        // 5. Match pyramid card with waste top
        if (wasteCard != null)
            for (int i = 0; i < expN; i++)
                if ((int)pyramid[exp[i]].Rank + (int)wasteCard.Rank == 13)
                    if (Solve(pMask & ~(1UL << exp[i]), stock, sPtr, wMask & ~(1UL << topWaste), passes, pyramid, seen, ref nodes)) return true;

        // 6. Match pyramid card with stock top
        if (stockCard != null)
            for (int i = 0; i < expN; i++)
                if ((int)pyramid[exp[i]].Rank + (int)stockCard.Rank == 13)
                    if (Solve(pMask & ~(1UL << exp[i]), stock, topStock, wMask, passes, pyramid, seen, ref nodes)) return true;

        // 7. Match stock top with waste top
        if (stockCard != null && wasteCard != null && (int)stockCard.Rank + (int)wasteCard.Rank == 13)
            if (Solve(pMask, stock, topStock, wMask & ~(1UL << topWaste), passes, pyramid, seen, ref nodes)) return true;

        // 8. Draw from stock (last resort — commits to this card being drawn)
        if (sPtr > 0)
            return Solve(pMask, stock, sPtr - 1, wMask | (1UL << (sPtr - 1)), passes, pyramid, seen, ref nodes);

        // 9. Recycle waste back to stock (if draws exhausted and redeals remain)
        if (passes < 2 && wMask != 0)
            return Solve(pMask, stock, stock.Length, 0UL, passes + 1, pyramid, seen, ref nodes);

        return false;
    }

    // For card at (r, c) with flat index idx: left child = idx + r + 1, right child = idx + r + 2
    private static int GetExposed(ulong pMask, Span<int> result)
    {
        int count = 0, idx = 0;
        for (int r = 0; r < 7; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                if ((pMask & (1UL << idx)) != 0)
                {
                    bool exposed = r == 6
                        || ((pMask & (1UL << (idx + r + 1))) == 0 && (pMask & (1UL << (idx + r + 2))) == 0);
                    if (exposed) result[count++] = idx;
                }
                idx++;
            }
        }
        return count;
    }
}
