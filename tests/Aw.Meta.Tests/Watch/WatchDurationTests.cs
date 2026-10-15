using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Watch;

public sealed class WatchDurationTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("29m", 29 * 60)]
    [InlineData("90s", 90)]
    [InlineData("2h", 7200)]
    [InlineData("120", 120)]
    [InlineData(" 5m ", 300)]
    public void A_duration_is_read_in_seconds(string text, int seconds)
    {
        Assert.True(WatchDuration.TryParse(text, out var duration));
        Assert.Equal(TimeSpan.FromSeconds(seconds), duration);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("m")]
    [InlineData("0")]
    [InlineData("-5m")]
    [InlineData("5d")]
    [InlineData("1.5h")]
    public void Anything_else_is_refused(string text) => Assert.False(WatchDuration.TryParse(text, out _));
}
