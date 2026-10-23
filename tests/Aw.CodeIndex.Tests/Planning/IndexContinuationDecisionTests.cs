using Aw.CodeIndex.Core.Planning;

namespace Aw.CodeIndex.Tests.Planning;

public sealed class IndexContinuationDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static IndexJobOutcome Job(int agoMinutes, int embedded = 0, bool pending = true, bool failed = false, bool deferred = false) =>
        new(_now.AddMinutes(-agoMinutes), failed, deferred, pending, embedded);

    private static ContinuationAction Decide(string head, string? recorded, params IndexJobOutcome[] newestFirst) =>
        IndexContinuationDecision.Decide(head, recorded, newestFirst, _now);

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_jobs_passes() => Assert.Equal(ContinuationAction.Pass, Decide("a", recorded: null));

    [Fact, Trait("Lane", "unit")]
    public void A_finished_build_with_nothing_pending_passes() => Assert.Equal(ContinuationAction.Pass, Decide("a", "a", Job(30, embedded: 10, pending: false)));

    [Fact, Trait("Lane", "unit")]
    public void After_a_job_that_embedded_something_the_repo_waits_two_minutes() =>
        Assert.Equal(ContinuationAction.Suppress, Decide("a", "a", Job(1, embedded: 100)));

    [Fact, Trait("Lane", "unit")]
    public void Two_minutes_after_a_job_that_embedded_something_the_recorded_head_is_withheld() =>
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(2, embedded: 100)));

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_after_a_job_that_embedded_something_passes() =>
        Assert.Equal(ContinuationAction.Pass, Decide("b", "a", Job(1, embedded: 100)));

    [Fact, Trait("Lane", "unit")]
    public void A_job_that_embedded_nothing_holds_ten_minutes_even_for_a_moved_head()
    {
        Assert.Equal(ContinuationAction.Suppress, Decide("a", "a", Job(9, embedded: 0)));
        Assert.Equal(ContinuationAction.Suppress, Decide("b", "a", Job(9, embedded: 0)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Ten_minutes_after_a_job_that_embedded_nothing_it_continues()
    {
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(10, embedded: 0)));
        Assert.Equal(ContinuationAction.Pass, Decide("b", "a", Job(10, embedded: 0)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_deferred_job_holds_ten_minutes_for_a_moved_head_too()
    {
        Assert.Equal(ContinuationAction.Suppress, Decide("b", "a", Job(5, deferred: true, pending: false)));
        Assert.Equal(ContinuationAction.Pass, Decide("b", "a", Job(10, deferred: true, pending: false)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Three_jobs_in_a_row_that_embedded_nothing_never_continue_until_the_head_moves()
    {
        var jobs = new[] { Job(60, 5), Job(120, 5), Job(180, 5), Job(240, 5) };

        Assert.Equal(ContinuationAction.Suppress, Decide("a", "a", [Job(60, embedded: 5), Job(120, embedded: 5), Job(180, embedded: 5), Job(240, embedded: 5)]));
        Assert.Equal(ContinuationAction.Pass, Decide("b", "a", jobs));
    }

    [Fact, Trait("Lane", "unit")]
    public void Progress_is_the_gain_over_the_last_published_job_so_a_gaining_job_is_not_counted_empty() =>
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(5, embedded: 30), Job(60, embedded: 20), Job(120, embedded: 20), Job(180, embedded: 20)));

    [Fact, Trait("Lane", "unit")]
    public void A_failed_continuation_is_retried_at_the_same_commit_and_left_to_the_shared_backoff() =>
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(1, failed: true, pending: false), Job(30, embedded: 5)));

    [Fact, Trait("Lane", "unit")]
    public void A_deferred_continuation_is_held_ten_minutes_then_retried()
    {
        Assert.Equal(ContinuationAction.Suppress, Decide("a", "a", Job(5, deferred: true, pending: false), Job(30, embedded: 5)));
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(10, deferred: true, pending: false), Job(30, embedded: 5)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_job_after_a_complete_build_passes() =>
        Assert.Equal(ContinuationAction.Pass, Decide("a", "a", Job(1, failed: true, pending: false), Job(30, embedded: 5, pending: false)));

    [Fact, Trait("Lane", "unit")]
    public void Embedded_counts_at_different_commits_are_not_compared()
    {
        var newest = new IndexJobOutcome(_now.AddMinutes(-3), Failed: false, Deferred: false, LayersPending: true, Embedded: 10, Commit: "b");
        var older = new IndexJobOutcome(_now.AddMinutes(-60), Failed: false, Deferred: false, LayersPending: true, Embedded: 20, Commit: "a");

        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("b", "b", newest, older));
    }

    [Fact, Trait("Lane", "unit")]
    public void Failed_and_deferred_jobs_between_are_skipped_when_reading_progress() =>
        Assert.Equal(ContinuationAction.WithholdRecorded, Decide("a", "a", Job(3, embedded: 9), Job(40, failed: true, pending: false), Job(50, embedded: 4)));
}
