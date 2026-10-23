using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Who started a run and when (workflows spec §7): the words, the clock, and what a row says when either is not served.</summary>
[Lane(TestLane.Unit)]
public sealed class StartedByTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);

    private static InstanceRowViewModel Row(string? by, DateTimeOffset? at) =>
        new(FixtureInstances.Rows[0] with { StartedBy = by, StartedAt = at }, new FakeClipboard(), new FakeTimeProvider(_now));

    [Theory]
    [InlineData("human:cli", "you")]
    [InlineData("human:app", "you")]
    [InlineData("session:abc", "an agent session")]
    [InlineData("service:host", "Sindri")]
    [InlineData("service:heartbeat", "Sindri's heartbeat service")]
    [InlineData("robot", "robot")]
    public void The_starter_is_said_in_words(string served, string words) => Assert.Equal(words, Labels.StartedByWords(served));

    [Fact]
    public void A_start_today_says_the_time_and_one_on_another_day_says_the_date_too()
    {
        Assert.Equal("11:02", Labels.StartedClock(new DateTimeOffset(2026, 10, 12, 11, 2, 0, TimeSpan.Zero), _now));
        Assert.Equal("Oct 11, 23:59", Labels.StartedClock(new DateTimeOffset(2026, 10, 11, 23, 59, 0, TimeSpan.Zero), _now));
    }

    [Theory]
    [InlineData("11:02", "human:cli", "Started 11:02 by you")]
    [InlineData("11:02", null, "Started 11:02")]
    [InlineData(null, "session:abc", "Started by an agent session")]
    [InlineData(null, null, "")]
    [InlineData("", "", "")]
    public void A_part_that_is_not_served_is_left_out(string? clock, string? by, string words) => Assert.Equal(words, Labels.WorkflowStarted(clock, by));

    [Fact]
    public void A_row_says_when_and_by_whom_in_the_persons_time_zone()
    {
        var row = Row("human:cli", new DateTimeOffset(2026, 10, 12, 11, 2, 0, TimeSpan.Zero));

        Assert.Equal("Started 11:02 by you", row.StartedText);
        Assert.Contains("Started 11:02 by you", row.AccessibleName, StringComparison.Ordinal);
        Assert.True(row.StartedByYou);
    }

    [Fact]
    public void A_row_with_neither_says_nothing_and_an_agent_started_row_is_not_yours()
    {
        Assert.Equal(string.Empty, Row(by: null, at: null).StartedText);
        Assert.False(Row("session:abc", new DateTimeOffset(2026, 10, 12, 11, 2, 0, TimeSpan.Zero)).StartedByYou);
        Assert.False(Row(by: null, at: null).StartedByYou);
    }
}
