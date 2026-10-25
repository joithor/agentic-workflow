using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The per-repo outlook of the map publish gate (code index spec 6.4), from the stored index heads, the service state and the repo's newest jobs.</summary>
public sealed class IndexOutlookRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static IndexAttempt Failed(int minutesAgo) => new(Failed: true, _now.AddMinutes(-minutesAgo));

    private static IndexAttempt Ok(int minutesAgo) => new(Failed: false, _now.AddMinutes(-minutesAgo));

    private static IndexOutlookDetail Classify(
        string? index = "old0000",
        string hold = "new1111",
        bool onDefault = true,
        string? live = "new1111",
        bool inScope = true,
        bool active = true,
        bool running = false,
        bool neverBuilt = false,
        params IndexAttempt[] finished) =>
        IndexOutlookRules.Classify(new IndexOutlookInput(index, hold, onDefault, live, inScope, active, running, neverBuilt, finished), _now);

    [Fact, Trait("Lane", "unit")]
    public void An_index_at_the_holds_commit_is_Built() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Built, RepoWait.None, RetryAt: null), Classify(index: "new1111"));

    [Fact, Trait("Lane", "unit")]
    public void Built_outranks_everything_a_paused_service_out_of_scope_and_three_failures() =>
        Assert.Equal(IndexOutlook.Built, Classify(index: "new1111", inScope: false, active: false, onDefault: false, neverBuilt: true, running: true, finished: [Failed(1), Failed(2), Failed(3)]).Outlook);

    [Fact, Trait("Lane", "unit")]
    public void A_build_running_is_Coming_Building() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Coming, RepoWait.Building, RetryAt: null), Classify(running: true));

    [Fact, Trait("Lane", "unit")]
    public void An_index_at_another_commit_with_nothing_due_is_Coming_Queued() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Coming, RepoWait.Queued, RetryAt: null), Classify(finished: Ok(30)));

    [Fact, Trait("Lane", "unit")]
    public void An_index_at_another_commit_with_the_head_moved_past_the_hold_is_Coming_NewerIndex() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Coming, RepoWait.NewerIndex, RetryAt: null), Classify(index: "newer22", live: "newer22"));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_or_damaged_index_that_was_built_before_is_Coming_Damaged() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Coming, RepoWait.Damaged, RetryAt: null), Classify(index: null, finished: Ok(30)));

    [Fact, Trait("Lane", "unit")]
    public void A_running_build_outranks_the_damaged_and_newer_words() =>
        Assert.Equal(RepoWait.Building, Classify(index: null, live: "newer22", running: true).Word);

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 2, 5)]
    [InlineData(2, 1, 10)]
    public void One_or_two_failures_inside_the_backoff_are_Backoff_Retrying_with_the_retry_time(int failures, int minutesAgo, int waitMinutes)
    {
        var attempts = Enumerable.Range(0, failures).Select(i => Failed(minutesAgo + i)).ToArray();
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Backoff, RepoWait.Retrying, _now.AddMinutes(-minutesAgo + waitMinutes)), Classify(finished: attempts));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failure_whose_backoff_is_over_is_due_again_and_so_Coming_Queued() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Coming, RepoWait.Queued, RetryAt: null), Classify(finished: Failed(6)));

    [Fact, Trait("Lane", "unit")]
    public void A_running_retry_is_Coming_not_Backoff() =>
        Assert.Equal(IndexOutlook.Coming, Classify(running: true, finished: Failed(1)).Outlook);

    [Fact, Trait("Lane", "unit")]
    public void Three_failures_in_a_row_are_Broken_without_a_retry_time() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Broken, RepoWait.None, RetryAt: null), Classify(finished: [Failed(1), Failed(2), Failed(3)]));

    [Fact, Trait("Lane", "unit")]
    public void A_success_between_failures_resets_the_count()
    {
        Assert.Equal(IndexOutlook.Backoff, Classify(finished: [Failed(1), Failed(2), Ok(3), Failed(4), Failed(5)]).Outlook);
        Assert.Equal(IndexOutlook.Coming, Classify(finished: [Ok(30), Failed(31), Failed(32), Failed(33)]).Outlook);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_failure_count_is_the_services_alarm_number() =>
        Assert.Equal(3, IndexOutlookRules.FailuresToBroken);

    [Fact, Trait("Lane", "unit")]
    public void A_paused_service_is_Absent_even_while_the_index_is_at_another_commit() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Absent, RepoWait.None, RetryAt: null), Classify(active: false, finished: Failed(1)));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_out_of_scope_is_Absent_NotIndexed() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Absent, RepoWait.NotIndexed, RetryAt: null), Classify(inScope: false));

    [Fact, Trait("Lane", "unit")]
    public void A_hold_head_off_the_default_branch_is_Absent() =>
        Assert.Equal(IndexOutlook.Absent, Classify(onDefault: false).Outlook);

    [Fact, Trait("Lane", "unit")]
    public void A_first_build_is_Absent_NotIndexed_and_outranks_Broken() =>
        Assert.Equal(new IndexOutlookDetail(IndexOutlook.Absent, RepoWait.NotIndexed, RetryAt: null), Classify(index: null, neverBuilt: true, finished: [Failed(1), Failed(2), Failed(3)]));

    [Fact, Trait("Lane", "unit")]
    public void A_first_build_that_is_running_is_still_not_waited_for() =>
        Assert.Equal(IndexOutlook.Absent, Classify(index: null, neverBuilt: true, running: true).Outlook);
}
