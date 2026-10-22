using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Features.BugFix;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>'step.bugfix.record-candidate': the diff's branch and worktree become bugfix-state's next candidate, and 'fact:candidate' carries what the recheck and the judge need.</summary>
[Lane(TestLane.Unit)]
public sealed class RecordCandidateStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_diff_is_recorded_as_a_candidate_with_its_head_attempt_and_files()
    {
        var rig = new BugFixRig();
        _ = await rig.State.StartAttempt(rig.StateDirectory, AttemptMode.Whole, Token);

        var result = await new RecordCandidateStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.record-candidate", ("diff", BugFixRig.Diff("fix-1"))), Token);

        Assert.Equal("recorded", result.Result!.Outcome.Value);
        var candidate = BugFixRig.Out(result, "candidate")!;
        Assert.Equal("c1", (string?)candidate["id"]);
        Assert.Equal("commit-of-fix-1", (string?)candidate["head"]);
        Assert.Equal(1, (int?)candidate["attempt"]);
        Assert.Equal("fix-1", (string?)candidate["branch"]);
        Assert.Equal("/work/fix-1", (string?)candidate["worktree"]);
        Assert.Equal(["src/app.txt"], candidate["changedFiles"]!.AsArray().Select(static f => (string?)f), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_hypothesis_and_the_test_change_approval_reach_bugfix_state()
    {
        var rig = new BugFixRig();
        _ = await rig.State.StartAttempt(rig.StateDirectory, AttemptMode.Whole, Token);

        _ = await new RecordCandidateStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.record-candidate", ("diff", """{"branch":"b","worktree":"/w","hypothesis":2,"allowTestChanges":true}""")), Token);

        Assert.Equal(new CandidateSource("b", "/w", Hypothesis: 2, AllowTestChanges: true), rig.State.Sources[0]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"branch":"b"}""")]
    [InlineData("\"just text\"")]
    public async Task A_diff_without_a_branch_and_a_worktree_fails_the_Step(string diff)
    {
        var rig = new BugFixRig();

        var result = await new RecordCandidateStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.record-candidate", ("diff", diff)), Token);

        Assert.Null(result.Result);
        Assert.Contains("branch", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.State.Calls);
    }

    [Fact]
    public async Task A_refusal_fails_the_Step_with_bugfix_states_code_and_reason()
    {
        var rig = new BugFixRig();   // no attempt is open

        var result = await new RecordCandidateStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.record-candidate", ("diff", BugFixRig.Diff("fix-1"))), Token);

        Assert.Null(result.Result);
        Assert.Contains("AW-BUGFIX-005", result.Failure, StringComparison.Ordinal);
    }
}
