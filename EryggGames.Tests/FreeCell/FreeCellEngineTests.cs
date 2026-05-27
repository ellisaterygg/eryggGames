using System.Collections.Generic;
using EryggGames.Core;
using EryggGames.FreeCell.Core;
using Xunit;

namespace EryggGames.Tests.FreeCell;

public class FreeCellEngineTests
{
    #region CalculateMaxMovable

    [Fact]
    public void CalculateMaxMovable_AllFull_Returns1()
    {
        var state = new FreeCellState();
        for (int i = 0; i < 8; i++) state.Tableau[i].Add(new CardModel(Suit.Clubs, Rank.King));
        for (int i = 0; i < 4; i++) state.FreeCells[i].Add(new CardModel(Suit.Diamonds, Rank.King));

        Assert.Equal(1, FreeCellEngine.CalculateMaxMovable(state));
    }

    [Fact]
    public void CalculateMaxMovable_OneFreeCell_Returns2()
    {
        var state = new FreeCellState();
        for (int i = 0; i < 8; i++) state.Tableau[i].Add(new CardModel(Suit.Clubs, Rank.King));
        for (int i = 1; i < 4; i++) state.FreeCells[i].Add(new CardModel(Suit.Diamonds, Rank.King));

        Assert.Equal(2, FreeCellEngine.CalculateMaxMovable(state));
    }

    [Fact]
    public void CalculateMaxMovable_OneCellOneColumn_Returns4()
    {
        var state = new FreeCellState();
        for (int i = 1; i < 8; i++) state.Tableau[i].Add(new CardModel(Suit.Clubs, Rank.King));
        for (int i = 1; i < 4; i++) state.FreeCells[i].Add(new CardModel(Suit.Diamonds, Rank.King));

        Assert.Equal(4, FreeCellEngine.CalculateMaxMovable(state));
    }

    #endregion

    #region CanMove — Tableau

    [Fact]
    public void CanMove_ValidTableauMove_ReturnsValid()
    {
        var state = new FreeCellState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Nine) }, PileType.Tableau, 0);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanMove_WrongRank_ReturnsInvalid()
    {
        var state = new FreeCellState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Eight) }, PileType.Tableau, 0);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CanMove_SameColor_ReturnsInvalid()
    {
        var state = new FreeCellState();
        state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Clubs, Rank.Nine) }, PileType.Tableau, 0);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CanMove_ToEmptyTableau_ReturnsValid()
    {
        var state = new FreeCellState();

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.King) }, PileType.Tableau, 0);

        Assert.True(result.IsValid);
    }

    #endregion

    #region CanMove — Foundation

    [Fact]
    public void CanMove_AceToEmptyFoundation_ReturnsValid()
    {
        var state = new FreeCellState();

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Ace) }, PileType.Foundation, 0);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanMove_TwoToAceFoundation_ReturnsValid()
    {
        var state = new FreeCellState();
        state.Foundations[0].Add(new CardModel(Suit.Hearts, Rank.Ace));
        state.FoundationSuits[0] = Suit.Hearts;

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Two) }, PileType.Foundation, 0);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanMove_WrongSuitToFoundation_ReturnsInvalid()
    {
        var state = new FreeCellState();
        state.Foundations[0].Add(new CardModel(Suit.Hearts, Rank.Ace));
        state.FoundationSuits[0] = Suit.Hearts;

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Spades, Rank.Two) }, PileType.Foundation, 0);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CanMove_NonAceToEmptyFoundation_ReturnsInvalid()
    {
        var state = new FreeCellState();

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Two) }, PileType.Foundation, 0);

        Assert.False(result.IsValid);
    }

    #endregion

    #region CanMove — Stack size limits

    [Fact]
    public void should_reject_stack_move_when_source_vacated_and_count_exceeds_max()
    {
        var state = BuildStateForStackSizeTest();
        var cards = new[]
        {
            new CardModel(Suit.Spades, Rank.Nine),
            new CardModel(Suit.Hearts, Rank.Eight),
            new CardModel(Suit.Spades, Rank.Seven),
            new CardModel(Suit.Hearts, Rank.Six),
            new CardModel(Suit.Spades, Rank.Five),
            new CardModel(Suit.Hearts, Rank.Four),
            new CardModel(Suit.Spades, Rank.Three)
        };

        var result = FreeCellEngine.CanMove(state, cards, PileType.Tableau, 1, sourceTableauIndex: 0);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void should_allow_stack_move_when_source_vacated_and_count_equals_max()
    {
        var state = BuildStateForStackSizeTest();
        var cards = new[]
        {
            new CardModel(Suit.Spades, Rank.Nine),
            new CardModel(Suit.Hearts, Rank.Eight),
            new CardModel(Suit.Spades, Rank.Seven),
            new CardModel(Suit.Hearts, Rank.Six),
            new CardModel(Suit.Spades, Rank.Five),
            new CardModel(Suit.Hearts, Rank.Four)
        };

        var result = FreeCellEngine.CanMove(state, cards, PileType.Tableau, 1, sourceTableauIndex: 0);

        Assert.True(result.IsValid);
    }

    private static FreeCellState BuildStateForStackSizeTest()
    {
        var state = new FreeCellState();
        state.Tableau[1].Add(new CardModel(Suit.Hearts, Rank.Ten));
        for (int i = 2; i <= 6; i++) state.Tableau[i].Add(new CardModel(Suit.Clubs, Rank.King));
        state.FreeCells[2].Add(new CardModel(Suit.Clubs, Rank.King));
        state.FreeCells[3].Add(new CardModel(Suit.Clubs, Rank.King));
        return state;
    }

    #endregion

    #region CanMove — FreeCell

    [Fact]
    public void CanMove_CardToEmptyFreeCell_ReturnsValid()
    {
        var state = new FreeCellState();

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Seven) }, PileType.FreeCell, 0);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanMove_CardToOccupiedFreeCell_ReturnsInvalid()
    {
        var state = new FreeCellState();
        state.FreeCells[0].Add(new CardModel(Suit.Clubs, Rank.Three));

        var result = FreeCellEngine.CanMove(state, new[] { new CardModel(Suit.Hearts, Rank.Seven) }, PileType.FreeCell, 0);

        Assert.False(result.IsValid);
    }

    #endregion

    #region IsValidSequence

    [Fact]
    public void IsValidSequence_AlternatingColorsDescending_ReturnsTrue()
    {
        var cards = new List<CardModel>
        {
            new CardModel(Suit.Spades, Rank.Ten),
            new CardModel(Suit.Hearts, Rank.Nine),
            new CardModel(Suit.Clubs, Rank.Eight)
        };

        Assert.True(FreeCellEngine.IsValidSequence(cards));
    }

    [Fact]
    public void IsValidSequence_SameColor_ReturnsFalse()
    {
        var cards = new List<CardModel>
        {
            new CardModel(Suit.Spades, Rank.Ten),
            new CardModel(Suit.Clubs, Rank.Nine)
        };

        Assert.False(FreeCellEngine.IsValidSequence(cards));
    }

    [Fact]
    public void IsValidSequence_SingleCard_ReturnsTrue()
    {
        var cards = new List<CardModel> { new CardModel(Suit.Hearts, Rank.Five) };

        Assert.True(FreeCellEngine.IsValidSequence(cards));
    }

    #endregion

    #region IsWon

    [Fact]
    public void IsWon_AllFoundationsFull_ReturnsTrue()
    {
        var state = new FreeCellState();
        foreach (Rank rank in System.Enum.GetValues<Rank>())
            for (int i = 0; i < 4; i++)
                state.Foundations[i].Add(new CardModel(Suit.Clubs, rank));

        Assert.True(FreeCellEngine.IsWon(state));
    }

    [Fact]
    public void IsWon_PartialFoundations_ReturnsFalse()
    {
        var state = new FreeCellState();
        state.Foundations[0].Add(new CardModel(Suit.Hearts, Rank.Ace));

        Assert.False(FreeCellEngine.IsWon(state));
    }

    #endregion
}
