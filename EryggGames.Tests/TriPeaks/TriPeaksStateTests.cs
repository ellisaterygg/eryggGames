using System.Text.Json;
using EryggGames.TriPeaks.Core;
using Xunit;

namespace EryggGames.Tests.TriPeaks;

public class TriPeaksStateTests
{
    #region Clone

    [Fact]
    public void should_preserve_winnable_only_false_when_cloned()
    {
        var state = new TriPeaksState { WinnableOnly = false };

        var clone = state.Clone();

        Assert.False(clone.WinnableOnly);
    }

    [Fact]
    public void should_preserve_winnable_only_true_when_cloned()
    {
        var state = new TriPeaksState { WinnableOnly = true };

        var clone = state.Clone();

        Assert.True(clone.WinnableOnly);
    }

    #endregion

    #region Serialization

    [Fact]
    public void should_round_trip_winnable_only_false_through_json()
    {
        var state = new TriPeaksState { WinnableOnly = false };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<TriPeaksState>(json);

        Assert.NotNull(loaded);
        Assert.False(loaded.WinnableOnly);
    }

    [Fact]
    public void should_round_trip_winnable_only_true_through_json()
    {
        var state = new TriPeaksState { WinnableOnly = true };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<TriPeaksState>(json);

        Assert.NotNull(loaded);
        Assert.True(loaded.WinnableOnly);
    }

    #endregion
}
