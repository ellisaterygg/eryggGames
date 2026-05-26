using System.Collections.Generic;
using EryggGames.Core;
using EryggGames.Klondike.Core;
using Xunit;

namespace EryggGames.Tests.Klondike;

public class KlondikeEngineTests
{
    #region CanMove — Tableau

    [Fact]
    public void CanMove_RedOnBlack_ReturnsTrue()
    {
        var state = new KlondikeState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Nine) }, PileType.Tableau, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanMove_BlackOnRed_ReturnsTrue()
    {
        var state = new KlondikeState();
        state.Tableau[0].Add(new CardModel(Suit.Hearts, Rank.Ten));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Clubs, Rank.Nine) }, PileType.Tableau, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanMove_SameColor_ReturnsFalse()
    {
        var state = new KlondikeState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Clubs, Rank.Nine) }, PileType.Tableau, 0);

        Assert.False(result);
    }

    [Fact]
    public void CanMove_WrongRank_ReturnsFalse()
    {
        var state = new KlondikeState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Eight) }, PileType.Tableau, 0);

        Assert.False(result);
    }

    [Fact]
    public void CanMove_KingToEmptyTableau_ReturnsTrue()
    {
        var state = new KlondikeState();

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Spades, Rank.King) }, PileType.Tableau, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanMove_NonKingToEmptyTableau_ReturnsFalse()
    {
        var state = new KlondikeState();

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Queen) }, PileType.Tableau, 0);

        Assert.False(result);
    }

    #endregion

    #region CanMove — Foundation

    [Fact]
    public void CanMove_AceToEmptyFoundation_ReturnsTrue()
    {
        var state = new KlondikeState();

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Ace) }, PileType.Foundation, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanMove_NonAceToEmptyFoundation_ReturnsFalse()
    {
        var state = new KlondikeState();

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Two) }, PileType.Foundation, 0);

        Assert.False(result);
    }

    [Fact]
    public void CanMove_CorrectSuitAndRankToFoundation_ReturnsTrue()
    {
        var state = new KlondikeState();
        state.Foundation[0].Add(new CardModel(Suit.Hearts, Rank.Ace));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Hearts, Rank.Two) }, PileType.Foundation, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanMove_WrongSuitToFoundation_ReturnsFalse()
    {
        var state = new KlondikeState();
        state.Foundation[0].Add(new CardModel(Suit.Hearts, Rank.Ace));

        var result = KlondikeEngine.CanMove(state, new List<CardModel> { new CardModel(Suit.Spades, Rank.Two) }, PileType.Foundation, 0);

        Assert.False(result);
    }

    [Fact]
    public void CanMove_MultipleCardsToFoundation_ReturnsFalse()
    {
        var state = new KlondikeState();

        var cards = new List<CardModel>
        {
            new CardModel(Suit.Hearts, Rank.Ace),
            new CardModel(Suit.Spades, Rank.King)
        };
        var result = KlondikeEngine.CanMove(state, cards, PileType.Foundation, 0);

        Assert.False(result);
    }

    #endregion

    #region IsWon

    [Fact]
    public void IsWon_AllFoundationsFull_ReturnsTrue()
    {
        var state = new KlondikeState();
        foreach (Rank rank in System.Enum.GetValues<Rank>())
            for (int i = 0; i < 4; i++)
                state.Foundation[i].Add(new CardModel(Suit.Clubs, rank));

        Assert.True(KlondikeEngine.IsWon(state));
    }

    [Fact]
    public void IsWon_PartialFoundations_ReturnsFalse()
    {
        var state = new KlondikeState();
        state.Foundation[0].Add(new CardModel(Suit.Hearts, Rank.Ace));

        Assert.False(KlondikeEngine.IsWon(state));
    }

    [Fact]
    public void IsWon_EmptyState_ReturnsFalse()
    {
        var state = new KlondikeState();

        Assert.False(KlondikeEngine.IsWon(state));
    }

    #endregion
}
