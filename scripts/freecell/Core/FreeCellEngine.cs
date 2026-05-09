using System;
using System.Collections.Generic;
using System.Linq;

namespace EryggGames.FreeCell.Core;

public static class FreeCellEngine
{
	public static bool IsValidSequence(IReadOnlyList<CardModel> cards)
	{
		if (cards.Count <= 1) return true;
		for (int i = 0; i < cards.Count - 1; i++)
		{
			var lower = cards[i];
			var upper = cards[i + 1];
			if (lower.IsRed == upper.IsRed) return false;
			if ((int)lower.Rank != (int)upper.Rank + 1) return false;
		}
		return true;
	}

	public static int CalculateMaxMovable(GameState state, int? excludeTableauIndex = null, int? destinationTableauIndex = null)
	{
		int emptyCells = state.FreeCells.Count(p => p.Count == 0);
		int emptyCols = 0;
		for (int i = 0; i < 8; i++)
		{
			if (i == excludeTableauIndex) continue;
			if (i == destinationTableauIndex && state.Tableau[i].Count == 0) continue;
			if (state.Tableau[i].Count == 0) emptyCols++;
		}

		return (emptyCells + 1) * (int)Math.Pow(2, Math.Max(0, emptyCols));
	}

	public static MoveResult CanMove(GameState state, IReadOnlyList<CardModel> movingCards, PileType targetType, int targetIndex)
	{
		if (movingCards.Count == 0) return new MoveResult(false, "No cards to move.");

		var bottomCard = movingCards[0];

		switch (targetType)
		{
			case PileType.Tableau:
				var targetCol = state.Tableau[targetIndex];
				if (targetCol.Count > 0)
				{
					var top = targetCol[^1];
					if (bottomCard.IsRed == top.IsRed) return new MoveResult(false, "Must alternate colors.");
					if ((int)bottomCard.Rank != (int)top.Rank - 1) return new MoveResult(false, "Must be one rank lower.");
				}
				int max = CalculateMaxMovable(state, null, targetIndex);
				if (movingCards.Count > max) return new MoveResult(false, $"Too many cards to move (Max: {max}).");
				return new MoveResult(true);

			case PileType.FreeCell:
				if (movingCards.Count > 1) return new MoveResult(false, "Only one card can move to a free cell.");
				if (state.FreeCells[targetIndex].Count > 0) return new MoveResult(false, "Free cell is occupied.");
				return new MoveResult(true);

			case PileType.Foundation:
				if (movingCards.Count > 1) return new MoveResult(false, "Only one card can move to a foundation.");
				var foundation = state.Foundations[targetIndex];
				if (foundation.Count == 0)
				{
					if (bottomCard.Rank != Rank.Ace) return new MoveResult(false, "Only Ace can start a foundation.");
					return new MoveResult(true);
				}
				else
				{
					if (bottomCard.Suit != state.FoundationSuits[targetIndex]) return new MoveResult(false, "Suit mismatch.");
					if ((int)bottomCard.Rank != (int)foundation[^1].Rank + 1) return new MoveResult(false, "Must be next rank up.");
					return new MoveResult(true);
				}

			default:
				return new MoveResult(false, "Unknown pile type.");
		}
	}

	public static bool IsWon(GameState state)
	{
		return state.Foundations.All(f => f.Count == 13);
	}

	public static bool CanAutoComplete(GameState state)
	{
		var tempState = state.Clone();
		bool progress = true;
		while (progress)
		{
			progress = false;
			// Check Tableau and FreeCells
			var sourcePiles = tempState.Tableau.Concat(tempState.FreeCells).Where(p => p.Count > 0);
			foreach (var pile in sourcePiles)
			{
				var card = pile[^1];
				for (int i = 0; i < 4; i++)
				{
					var result = CanMove(tempState, new[] { card }, PileType.Foundation, i);
					if (result.IsValid)
					{
						if (tempState.Foundations[i].Count == 0) tempState.FoundationSuits[i] = card.Suit;
						pile.RemoveAt(pile.Count - 1);
						tempState.Foundations[i].Add(card);
						progress = true;
						break;
					}
				}
				if (progress) break;
			}
		}

		return tempState.Tableau.All(p => p.Count == 0) && tempState.FreeCells.All(p => p.Count == 0);
	}
}
