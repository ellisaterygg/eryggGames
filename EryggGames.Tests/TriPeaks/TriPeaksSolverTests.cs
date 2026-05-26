using EryggGames.Core;
using EryggGames.TriPeaks.Core;
using Xunit;

namespace EryggGames.Tests.TriPeaks;

public class TriPeaksSolverTests
{
    #region IsWinnable

    [Fact]
    public void IsWinnable_AllFivesNoMatches_ReturnsFalse()
    {
        var state = new TriPeaksState();
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < state.Peaks[r].Length; c++)
                state.Peaks[r][c] = new CardModel(Suit.Clubs, Rank.Five);

        state.Waste.Add(new CardModel(Suit.Hearts, Rank.Ace));

        Assert.False(TriPeaksSolver.IsWinnable(state));
    }

    #endregion
}

public class TriPeaksEngineTests
{
    #region IsValidMove

    [Fact]
    public void IsValidMove_AdjacentRankHigher_ReturnsTrue()
    {
        var card = new CardModel(Suit.Hearts, Rank.Seven);
        var wasteTop = new CardModel(Suit.Clubs, Rank.Six);

        Assert.True(TriPeaksEngine.IsValidMove(card, wasteTop));
    }

    [Fact]
    public void IsValidMove_AdjacentRankLower_ReturnsTrue()
    {
        var card = new CardModel(Suit.Hearts, Rank.Five);
        var wasteTop = new CardModel(Suit.Clubs, Rank.Six);

        Assert.True(TriPeaksEngine.IsValidMove(card, wasteTop));
    }

    [Fact]
    public void IsValidMove_NonAdjacent_ReturnsFalse()
    {
        var card = new CardModel(Suit.Hearts, Rank.Three);
        var wasteTop = new CardModel(Suit.Clubs, Rank.Six);

        Assert.False(TriPeaksEngine.IsValidMove(card, wasteTop));
    }

    [Fact]
    public void IsValidMove_AceAndKingWrapAround_ReturnsTrue()
    {
        var ace = new CardModel(Suit.Hearts, Rank.Ace);
        var king = new CardModel(Suit.Clubs, Rank.King);

        Assert.True(TriPeaksEngine.IsValidMove(ace, king));
        Assert.True(TriPeaksEngine.IsValidMove(king, ace));
    }

    #endregion

    #region IsWon

    [Fact]
    public void IsWon_AllPeaksCleared_ReturnsTrue()
    {
        var state = new TriPeaksState();

        Assert.True(TriPeaksEngine.IsWon(state));
    }

    [Fact]
    public void IsWon_OneCardRemaining_ReturnsFalse()
    {
        var state = new TriPeaksState();
        state.Peaks[3][0] = new CardModel(Suit.Clubs, Rank.Seven);

        Assert.False(TriPeaksEngine.IsWon(state));
    }

    #endregion
}
