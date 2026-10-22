using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Features.BugFix;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>'step.bugfix.judge': the verdict bugfix-state records, once per candidate. A failed check or no evidence can never come out resolved.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeStepTests
{
    private const string Candidate = """{"id":"c1","head":"commit-of-fix-1","attempt":1,"branch":"fix-1","worktree":"/work/fix-1","changedFiles":["src/app.txt"]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BugFixRig> Rechecked(bool fixes)
    {
        var rig = new BugFixRig();
        _ = fixes ? rig.State.Fixes("fix-1") : rig.State;
        _ = await rig.State.StartAttempt(rig.StateDirectory, AttemptMode.Whole, Token);
        _ = await rig.State.RecordCandidate(rig.StateDirectory, new CandidateSource("fix-1", "/work/fix-1"), Token);
        _ = await rig.State.Recheck(rig.StateDirectory, "c1", "/work/fix-1", new FrozenCheck("test", "t", ["t"], "."), Token);
        return rig;
    }

    private static Core.Workflow.Blocks.StepContext Context(params (string Key, string Json)[] extra) =>
        BugFixRig.Context("step.bugfix.judge", [("check", BugFixRig.Check), ("candidate", Candidate), ("evidenceSummary", BugFixRig.Evidence), .. extra]);

    [Theory]
    [InlineData(JudgeDecision.Resolved, "resolved")]
    [InlineData(JudgeDecision.Partial, "partial")]
    [InlineData(JudgeDecision.Unresolved, "unresolved")]
    [InlineData(JudgeDecision.Escalated, "needsHuman")]
    public async Task Each_decision_is_the_outcome_with_the_verdict_as_a_fact(JudgeDecision decision, string outcome)
    {
        var rig = await Rechecked(fixes: true);
        _ = rig.State.Judges(decision);

        var result = await new JudgeStep(rig.State, rig.Area).Run(Context(), Token);

        Assert.Equal(outcome, result.Result!.Outcome.Value);
        var verdict = BugFixRig.Out(result, "verdict")!;
        Assert.Equal("c1", (string?)verdict["candidate"]);
        Assert.Equal(outcome, (string?)verdict["decision"]);
        Assert.Equal("fake-reason", (string?)verdict["reasonCode"]);
        Assert.Equal(["the fake judged it"], verdict["reasons"]!.AsArray().Select(static r => (string?)r), StringComparer.Ordinal);
        Assert.True((bool?)verdict["asked"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task No_evidence_is_needsHuman_and_judge_is_never_asked(string evidence)
    {
        var rig = await Rechecked(fixes: true);

        var result = await new JudgeStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.judge", ("check", BugFixRig.Check), ("candidate", Candidate), ("evidenceSummary", evidence)), Token);

        Assert.Equal("needsHuman", result.Result!.Outcome.Value);
        var verdict = BugFixRig.Out(result, "verdict")!;
        Assert.Equal("no-evidence", (string?)verdict["reasonCode"]);
        Assert.False((bool?)verdict["asked"]);
        Assert.DoesNotContain("judge c1", rig.State.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_candidate_whose_check_failed_is_unresolved_never_resolved()
    {
        var rig = await Rechecked(fixes: false);
        _ = rig.State.Judges(JudgeDecision.Resolved);

        var result = await new JudgeStep(rig.State, rig.Area).Run(Context(), Token);

        Assert.Equal("unresolved", result.Result!.Outcome.Value);
        var verdict = BugFixRig.Out(result, "verdict")!;
        Assert.Equal("check-not-passed", (string?)verdict["reasonCode"]);
        Assert.False((bool?)verdict["asked"]);
    }

    [Fact]
    public async Task A_second_ask_for_the_same_candidate_fails_the_Step_and_is_not_a_verdict()
    {
        var rig = await Rechecked(fixes: true);
        var step = new JudgeStep(rig.State, rig.Area);
        _ = await step.Run(Context(), Token);

        var again = await step.Run(Context(), Token);

        Assert.Null(again.Result);
        Assert.Contains("already judged", again.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"c1\"")]
    public async Task A_candidate_without_an_id_fails_the_Step(string candidate)
    {
        var rig = await Rechecked(fixes: true);

        var result = await new JudgeStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.judge", ("check", BugFixRig.Check), ("candidate", candidate), ("evidenceSummary", BugFixRig.Evidence)), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:candidate", result.Failure, StringComparison.Ordinal);
    }
}
