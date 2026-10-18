using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.LaneContexts;

public sealed class ContextWatchTests
{
    private static LaneContext Lane(string lane, long? tokens, ContextVerdict verdict) =>
        new(lane, SessionId.From("s"), "/t", tokens, LastActivity: null, 300_000, verdict);

    [Fact, Trait("Lane", "unit")]
    public void A_crossing_is_reported_once_and_not_again_while_the_lane_stays_high()
    {
        var (Lines, High) = ContextWatch.Step(ContextWatch.Parse(null), [Lane("api", 487_000, ContextVerdict.Recycle)]);
        var second = ContextWatch.Step(High, [Lane("api", 495_000, ContextVerdict.Recycle)]);

        Assert.Equal(["CONTEXT HIGH api 487k"], Lines);
        Assert.Empty(second.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_re_armed_watch_that_reads_the_kept_state_does_not_repeat_a_standing_high()
    {
        var kept = ContextWatch.Format(ContextWatch.Step(ContextWatch.Parse(null), [Lane("api", 400_000, ContextVerdict.Recycle)]).High);
        var (Lines, _) = ContextWatch.Step(ContextWatch.Parse(kept), [Lane("api", 410_000, ContextVerdict.Recycle)]);

        Assert.Empty(Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void After_a_recycle_the_lane_reports_ok_and_can_cross_again()
    {
        var high = ContextWatch.Step(ContextWatch.Parse(null), [Lane("api", 400_000, ContextVerdict.Recycle)]).High;

        var (Lines, High) = ContextWatch.Step(high, [Lane("api", 9_000, ContextVerdict.Ok)]);
        var again = ContextWatch.Step(High, [Lane("api", 350_000, ContextVerdict.Recycle)]);

        Assert.Equal(["CONTEXT OK api"], Lines);
        Assert.Equal(["CONTEXT HIGH api 350k"], again.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_whose_size_cannot_be_told_keeps_its_state_and_says_nothing()
    {
        var high = ContextWatch.Step(ContextWatch.Parse(null), [Lane("api", 400_000, ContextVerdict.Recycle)]).High;

        var (Lines, High) = ContextWatch.Step(high, [Lane("api", tokens: null, ContextVerdict.Unknown)]);

        Assert.Empty(Lines);
        Assert.Contains("api", High);
    }
}
