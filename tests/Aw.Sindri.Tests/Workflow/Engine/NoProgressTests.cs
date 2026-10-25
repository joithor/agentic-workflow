using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// No-progress (spec §3.7, A §17.23.13): round n makes no progress over round n-1 when its findings equal round n-1's,
/// when it produced an empty diff, or when the same check failed twice with the same failure signature. Only the
/// signals the loop names count; the first round has nothing to compare with.
/// </summary>
public sealed class NoProgressTests
{
    private static readonly LoopSpec _all = new(3, LoopSpec.DefaultNoProgress);

    [Fact, Trait("Lane", "unit")]
    public void Same_findings_twice_is_no_progress()
    {
        Assert.True(LoopRules.NoProgress(_all, Findings("A"), Findings("A")));
        Assert.False(LoopRules.NoProgress(_all, Findings("A"), Findings("B")));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_diff_is_no_progress()
    {
        Assert.True(LoopRules.NoProgress(_all, Findings("A"), EmptyDiff));
        Assert.True(LoopRules.NoProgress(_all, Findings("A"), Findings("B") with { EmptyDiff = true }));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_failure_twice_is_no_progress()
    {
        Assert.True(LoopRules.NoProgress(_all, Failure("exit 1: VersionFlag"), Failure("exit 1: VersionFlag")));
        Assert.False(LoopRules.NoProgress(_all, Failure("exit 1: VersionFlag"), Failure("exit 1: ParserTests")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_signals_the_loop_names_count()
    {
        var findingsOnly = new LoopSpec(3, [NoProgressSignal.Findings]);
        var emptyDiffOnly = new LoopSpec(3, [NoProgressSignal.EmptyDiff]);
        var sameFailureOnly = new LoopSpec(3, [NoProgressSignal.SameFailure]);

        Assert.False(LoopRules.NoProgress(findingsOnly, Failure("F"), Failure("F")));
        Assert.False(LoopRules.NoProgress(findingsOnly, Findings("A"), EmptyDiff));
        Assert.False(LoopRules.NoProgress(emptyDiffOnly, Findings("A"), Findings("A")));
        Assert.False(LoopRules.NoProgress(sameFailureOnly, Findings("A"), Findings("A")));
        Assert.True(LoopRules.NoProgress(sameFailureOnly, Failure("F"), Failure("F")));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_round_has_nothing_to_compare_with()
    {
        Assert.False(LoopRules.NoProgress(_all, previous: null, Findings("A")));
        Assert.False(LoopRules.NoProgress(_all, previous: null, EmptyDiff));
        Assert.False(LoopRules.NoProgress(_all, previous: null, Failure("F")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Absent_findings_and_failures_never_match()
    {
        var nothing = new ProgressMark(Findings: null, Failure: null, EmptyDiff: false);

        Assert.False(LoopRules.NoProgress(_all, nothing, nothing));
        Assert.False(LoopRules.NoProgress(_all, Findings("A"), Failure("A")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Workflow_step_no_progress_case_parks_when_checks_fail_twice_with_the_same_findings()
    {
        // no-progress.jsonl: produce (round 1) -> checks failed (findings X) -> produce (2) -> checks failed (X again) -> parked: no-progress
        var findings = Findings("test VersionFlag_prints_the_version fails: printed nothing");
        var first = LoopRules.Reenter(In("checks", new LoopState(1, 3, Last: null)), StepRecipe, "produce", findings);
        Assert.Null(first.Park);

        var second = LoopRules.Reenter(In("checks", first.Next), StepRecipe, "produce", findings);

        Assert.Null(second.Next);
        var park = second.Park!; // why: the second identical failure parks; Assert.Equal below fails first if not
        Assert.Equal(ParkReason.NoProgress, park.Reason);
        Assert.Equal(ParkKind.Loop, park.ParkKind);
        Assert.Equal(ResumeKind.Transition, park.Resume.Kind);
        Assert.Equal("round 2 of revisions made no progress over round 1 (the same findings twice)", park.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_progress_parks_before_the_rounds_run_out()
    {
        var verdict = LoopRules.Reenter(In("checks", new LoopState(2, 3, Findings("A"))), StepRecipe, "produce", EmptyDiff);

        Assert.Equal(ParkReason.NoProgress, verdict.Park?.Reason);
        Assert.Equal("round 2 of revisions made no progress over round 1 (an empty diff)", verdict.Park?.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_progress_wins_over_rounds_at_the_cap()
    {
        var verdict = LoopRules.Reenter(In("checks", new LoopState(3, 3, Failure("F"))), StepRecipe, "produce", Failure("F"));

        Assert.Equal(ParkReason.NoProgress, verdict.Park?.Reason);
        Assert.Equal("round 3 of revisions made no progress over round 2 (the same failure twice)", verdict.Park?.Question);
    }

    private static ProgressMark Keyed(params string[] keys) => new(Findings: "digest-" + string.Concat(keys), Failure: null, EmptyDiff: false, FindingKeys: keys);

    [Fact, Trait("Lane", "unit")]
    public void A_superset_of_last_rounds_findings_is_no_progress()
    {
        Assert.True(LoopRules.NoProgress(_all, Keyed("a", "b"), Keyed("a", "b", "c")));
        Assert.True(LoopRules.NoProgress(_all, Keyed("a", "b"), Keyed("a", "b")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fewer_findings_is_progress()
    {
        Assert.False(LoopRules.NoProgress(_all, Keyed("a", "b"), Keyed("a")));
        Assert.False(LoopRules.NoProgress(_all, Keyed("a", "b"), Keyed("a", "c")));
        Assert.False(LoopRules.NoProgress(_all, Keyed("a"), Keyed()));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_diff_parks_when_the_loop_names_it()
    {
        var verdict = LoopRules.Reenter(In("checks", new LoopState(1, 3, Keyed("a"))), StepRecipe, "produce", Keyed("z") with { EmptyDiff = true });

        Assert.Equal(ParkReason.NoProgress, verdict.Park?.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void Findings_without_keys_keep_the_digest_rule()
    {
        Assert.True(LoopRules.NoProgress(_all, Findings("A"), Findings("A")));
        Assert.False(LoopRules.NoProgress(_all, Findings("A"), Keyed("a")));
        Assert.True(LoopRules.NoProgress(_all, Keyed("a") with { FindingKeys = null }, Keyed("a") with { FindingKeys = null }));
    }
}
