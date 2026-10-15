using Aw.Meta.Core.Messages;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Messages;

public sealed class DueTimeTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("+30m", 30)]
    [InlineData("+2h", 120)]
    [InlineData(" +1d ", 1440)]
    public void A_relative_time_counts_from_now(string text, int minutes) =>
        Assert.Equal(A.T0.AddMinutes(minutes), DueTime.Parse(text, A.T0));

    [Fact, Trait("Lane", "unit")]
    public void Seconds_are_a_relative_unit_too() => Assert.Equal(A.T0.AddSeconds(90), DueTime.Parse("+90s", A.T0));

    [Fact, Trait("Lane", "unit")]
    public void An_ISO_time_is_read_as_given_and_utc_when_it_has_no_offset()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero), DueTime.Parse("2026-10-15T09:00:00Z", A.T0));
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero), DueTime.Parse("2026-10-15T09:00:00", A.T0));
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 7, 0, 0, TimeSpan.Zero), DueTime.Parse("2026-10-15T09:00:00+02:00", A.T0));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("+m")]
    [InlineData("+5x")]
    [InlineData("+-5m")]
    public void Anything_else_is_not_a_time(string text) => Assert.Null(DueTime.Parse(text, A.T0));
}
