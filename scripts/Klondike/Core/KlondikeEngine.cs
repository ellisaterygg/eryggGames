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

    public static bool IsSafeToMoveToFoundation(KlondikeState state, CardModel card)
    {
        // Aces and Twos are always safe
        if (card.Rank <= Rank.Two) return true;

        // A card of rank R is safe to move if it is no longer "useful" as a base for stacks.
        // For Klondike, a card is useless if all cards of rank R-1 that could sit on it 
        // (which must be of the opposite color) are already foundation-ready.
        // A card is foundation-ready if its rank R-2 is already filled in the foundation.
        
        int requiredRank = (int)card.Rank - 2;
        
        // Find the ranks of the foundations for the opposite color suits
        // Clubs/Spades are black, Hearts/Diamonds are red.
        bool isRed = card.IsRed;
        
        int minOppositeRank = 13;
        for (int i = 0; i < 4; i++)
        {
            var foundation = state.Foundation[i];
            if (foundation.Count == 0)
            {
                minOppositeRank = 0;
                break;
            }
            
            var topCard = foundation[^1];
            if (topCard.IsRed != isRed)
            {
                minOppositeRank = Math.Min(minOppositeRank, foundation.Count);
            }
        }

        return minOppositeRank >= requiredRank;
    }
}
