using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Features.BugFix;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>'step.bugfix.recheck': the frozen check runs on the candidate through bugfix-state; passed or failed, with 'fact:recheck' either way.</summary>
[Lane(TestLane.Unit)]
public sealed class RecheckStepTests
{
    private const string Candidate = """{"id":"c1","head":"commit-of-fix-1","attempt":1,"branch":"fix-1","worktree":"/work/fix-1","changedFiles":["src/app.txt"]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BugFixRig> Recorded(bool fixes)
    {
        var rig = new BugFixRig();
        _ = fixes ? rig.State.Fixes("fix-1") : rig.State;
        _ = await rig.State.StartAttempt(rig.StateDirectory, AttemptMode.Whole, Token);
        _ = await rig.State.RecordCandidate(rig.StateDirectory, new CandidateSource("fix-1", "/work/fix-1"), Token);
        return rig;
    }

    [Theory]
    [InlineData(true, "passed", 0)]
    [InlineData(false, "failed", 1)]
    public async Task The_check_runs_on_the_candidates_worktree_and_the_run_is_reported(bool fixes, string outcome, int exitCode)
    {
        var rig = await Recorded(fixes);

        var result = await new RecheckStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.recheck", ("check", BugFixRig.Check), ("candidate", Candidate)), Token);

        Assert.Equal(outcome, result.Result!.Outcome.Value);
        var recheck = BugFixRig.Out(result, "recheck")!;
        Assert.Equal("c1", (string?)recheck["candidate"]);
        Assert.Equal(fixes, (bool?)recheck["passed"]);
        Assert.Equal(exitCode, (int?)recheck["exitCode"]);
        Assert.Equal("commit-of-fix-1", (string?)recheck["head"]);
        Assert.EndsWith("c1.json", (string?)recheck["evidence"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"kind":"test","file":"t"}""")]
    [InlineData("""{"kind":"manual","file":"t","command":["x"]}""")]
    [InlineData("\"text\"")]
    public async Task A_check_that_is_not_a_frozen_check_fails_the_Step(string check)
    {
        var rig = await Recorded(fixes: true);

        var result = await new RecheckStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.recheck", ("check", check), ("candidate", Candidate)), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:check", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"id":"c1"}""")]
    public async Task A_candidate_without_an_id_and_a_worktree_fails_the_Step(string candidate)
    {
        var rig = await Recorded(fixes: true);

        var result = await new RecheckStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.recheck", ("check", BugFixRig.Check), ("candidate", candidate)), Token);

        Assert.Null(result.Result);
        Assert.Contains("fact:candidate", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_candidate_bugfix_state_does_not_know_fails_the_Step_with_its_code()
    {
        var rig = new BugFixRig();

        var result = await new RecheckStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.recheck", ("check", BugFixRig.Check), ("candidate", Candidate)), Token);

        Assert.Null(result.Result);
        Assert.Contains("AW-BUGFIX-013", result.Failure, StringComparison.Ordinal);
    }
}
