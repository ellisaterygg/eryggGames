using System;
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Klondike.Core;

public static class KlondikeEngine
{
    public static bool CanMove(KlondikeState state, List<CardModel> movingCards, PileType targetType, int targetIdx)
    {
        if (movingCards.Count == 0) return false;
        var bottomCard = movingCards[0];

        if (targetType == PileType.Foundation)
        {
            if (movingCards.Count > 1) return false;
            var foundation = state.Foundation[targetIdx];
            if (foundation.Count == 0)
            {
                return bottomCard.Rank == Rank.Ace;
            }
            var top = foundation[^1];
            return bottomCard.Suit == top.Suit && bottomCard.Rank == top.Rank + 1;
        }

        if (targetType == PileType.Tableau)
        {
            var tableau = state.Tableau[targetIdx];
            if (tableau.Count == 0)
            {
                return bottomCard.Rank == Rank.King;
            }
            var top = tableau[^1];
            return bottomCard.IsRed != top.IsRed && bottomCard.Rank == top.Rank - 1;
        }

        return false;
    }

    public static bool IsWon(KlondikeState state)
    {
        return state.Foundation.All(f => f.Count == 13);
    }
}
