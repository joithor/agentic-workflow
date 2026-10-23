using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The embed decision (code index spec §6.3): a pending layer starts a job, with the right wait after each way the last one ended.</summary>
public sealed class EmbedDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static EmbedAttempt Ended(EmbedAttemptKind kind, TimeSpan ago) => new(kind, _now - ago);

    [Fact, Trait("Lane", "unit")]
    public void A_running_job_is_never_doubled()
        => Assert.Equal(MainMoveAction.JobRunning, EmbedDecision.Decide(pending: true, running: true, [], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_running_job_wins_even_when_the_layer_is_not_pending()
        => Assert.Equal(MainMoveAction.JobRunning, EmbedDecision.Decide(pending: false, running: true, [], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_layer_that_is_not_pending_starts_nothing()
        => Assert.Equal(MainMoveAction.Nothing, EmbedDecision.Decide(pending: false, running: false, [Ended(EmbedAttemptKind.Progress, TimeSpan.FromHours(1))], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_pending_layer_with_no_job_yet_starts_one()
        => Assert.Equal(MainMoveAction.Start, EmbedDecision.Decide(pending: true, running: false, [], _now));

    [Fact, Trait("Lane", "unit")]
    public void A_job_that_embedded_something_is_followed_at_once()
        => Assert.Equal(MainMoveAction.Start, EmbedDecision.Decide(pending: true, running: false, [Ended(EmbedAttemptKind.Progress, TimeSpan.Zero)], _now));

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 4, MainMoveAction.BackoffNotDue)]
    [InlineData(1, 6, MainMoveAction.Start)]
    public void A_failure_waits_five_minutes(int failures, int minutesAgo, MainMoveAction expected)
        => Assert.Equal(expected, EmbedDecision.Decide(pending: true, running: false, Failed(failures, minutesAgo), _now));

    [Theory, Trait("Lane", "unit")]
    [InlineData(2, 9, MainMoveAction.BackoffNotDue)]
    [InlineData(2, 11, MainMoveAction.Start)]
    [InlineData(3, 19, MainMoveAction.BackoffNotDue)]
    [InlineData(3, 21, MainMoveAction.Start)]
    public void Failures_in_a_row_double_the_wait(int failures, int minutesAgo, MainMoveAction expected)
        => Assert.Equal(expected, EmbedDecision.Decide(pending: true, running: false, Failed(failures, minutesAgo), _now));

    [Fact, Trait("Lane", "unit")]
    public void A_success_between_failures_resets_the_count()
    {
        var finished = new[] { Ended(EmbedAttemptKind.Failed, TimeSpan.FromMinutes(6)), Ended(EmbedAttemptKind.Progress, TimeSpan.FromMinutes(20)), Ended(EmbedAttemptKind.Failed, TimeSpan.FromMinutes(30)) };

        Assert.Equal(1, EmbedDecision.FailedInARow(finished));
        Assert.Equal(MainMoveAction.Start, EmbedDecision.Decide(pending: true, running: false, finished, _now));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, MainMoveAction.BackoffNotDue)]
    [InlineData(3, MainMoveAction.Start)]
    public void A_busy_or_unbuilt_index_is_a_short_wait_not_a_failure(int minutesAgo, MainMoveAction expected)
    {
        var finished = new[] { Ended(EmbedAttemptKind.TryLater, TimeSpan.FromMinutes(minutesAgo)) };

        Assert.Equal(0, EmbedDecision.FailedInARow(finished));
        Assert.Equal(expected, EmbedDecision.Decide(pending: true, running: false, finished, _now));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(9, MainMoveAction.BackoffNotDue)]
    [InlineData(11, MainMoveAction.Start)]
    public void A_job_that_left_the_layer_pending_without_progress_waits_ten_minutes(int minutesAgo, MainMoveAction expected)
        => Assert.Equal(expected, EmbedDecision.Decide(pending: true, running: false, [Ended(EmbedAttemptKind.NoProgress, TimeSpan.FromMinutes(minutesAgo))], _now));

    private static List<EmbedAttempt> Failed(int count, int newestMinutesAgo)
        => [.. Enumerable.Range(0, count).Select(i => Ended(EmbedAttemptKind.Failed, TimeSpan.FromMinutes(newestMinutesAgo + (i * 30))))];
}
