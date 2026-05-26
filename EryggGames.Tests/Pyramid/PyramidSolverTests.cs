using EryggGames.Core;
using EryggGames.Pyramid.Core;
using Xunit;

namespace EryggGames.Tests.Pyramid;

public class PyramidSolverTests
{
    #region IsWinnable

    [Fact]
    public void IsWinnable_AllKings_ReturnsTrue()
    {
        var state = new PyramidState();
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.King);

        Assert.True(PyramidSolver.IsWinnable(state));
    }

    [Fact]
    public void IsWinnable_AllTwos_ReturnsFalse()
    {
        var state = new PyramidState();
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.Two);

        Assert.False(PyramidSolver.IsWinnable(state));
    }

    [Fact]
    public void IsWinnable_ConstructedKingPyramid_ReturnsTrue()
    {
        var state = new PyramidState();
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++)
                state.Pyramid[r][c] = new CardModel(Suit.Clubs, Rank.King);

        Assert.True(PyramidSolver.IsWinnable(state));
    }

    #endregion
}
