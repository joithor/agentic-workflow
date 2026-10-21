using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class SyncGapDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static SyncAttempt Failed(int minutesAgo) => new(Failed: true, _now.AddMinutes(-minutesAgo));

    private static SyncAttempt Ok(int minutesAgo) => new(Failed: false, _now.AddMinutes(-minutesAgo));

    [Fact, Trait("Lane", "unit")]
    public void No_gap_is_nothing_even_with_a_job_running_or_failures() =>
        Assert.Equal(SyncGapAction.Nothing, SyncGapDecision.Decide(gap: false, running: true, [Failed(1)], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_gap_with_nothing_tried_starts() =>
        Assert.Equal(SyncGapAction.Start, SyncGapDecision.Decide(gap: true, running: false, [], _now));

    [Fact, Trait("Lane", "unit")]
    public void Job_still_running_means_no_start() =>
        Assert.Equal(SyncGapAction.JobRunning, SyncGapDecision.Decide(gap: true, running: true, [], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_finished_ok_sync_that_left_the_gap_starts_again() =>
        Assert.Equal(SyncGapAction.Start, SyncGapDecision.Decide(gap: true, running: false, [Ok(1)], _now));

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(6, 60)]
    [InlineData(40, 60)]
    public void Backoff_doubles_from_5_minutes_and_caps_at_60(int failuresInARow, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), SyncGapDecision.Wait(failuresInARow));
        var history = Enumerable.Range(0, failuresInARow).Select(i => Failed(minutes - 1 + i)).ToList();   // the newest ended one minute short of the wait
        var due = Enumerable.Range(0, failuresInARow).Select(i => Failed(minutes + i)).ToList();

        Assert.Equal(SyncGapAction.BackoffNotDue, SyncGapDecision.Decide(gap: true, running: false, history, _now));
        Assert.Equal(SyncGapAction.Start, SyncGapDecision.Decide(gap: true, running: false, due, _now));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_success_resets_the_count()
    {
        var afterSuccess = new[] { Failed(8), Ok(9), Failed(10), Failed(11) };   // one failure since the success: 5 minutes, due

        Assert.Equal(SyncGapAction.Start, SyncGapDecision.Decide(gap: true, running: false, afterSuccess, _now));
    }
}
