using System.Text.Json;
using EryggGames.Pyramid.Core;
using Xunit;

namespace EryggGames.Tests.Pyramid;

public class PyramidStateTests
{
    #region Clone

    [Fact]
    public void should_preserve_winnable_only_false_when_cloned()
    {
        var state = new PyramidState { WinnableOnly = false };

        var clone = state.Clone();

        Assert.False(clone.WinnableOnly);
    }

    [Fact]
    public void should_preserve_winnable_only_true_when_cloned()
    {
        var state = new PyramidState { WinnableOnly = true };

        var clone = state.Clone();

        Assert.True(clone.WinnableOnly);
    }

    #endregion

    #region Serialization

    [Fact]
    public void should_round_trip_winnable_only_false_through_json()
    {
        var state = new PyramidState { WinnableOnly = false };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<PyramidState>(json);

        Assert.NotNull(loaded);
        Assert.False(loaded.WinnableOnly);
    }

    [Fact]
    public void should_round_trip_winnable_only_true_through_json()
    {
        var state = new PyramidState { WinnableOnly = true };

        var json = JsonSerializer.Serialize(state);
        var loaded = JsonSerializer.Deserialize<PyramidState>(json);

        Assert.NotNull(loaded);
        Assert.True(loaded.WinnableOnly);
    }

    #endregion
}
