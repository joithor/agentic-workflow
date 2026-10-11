using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.StartAttempt;
using Aw.BugFix.Testing;
using Aw.Kernel;

namespace Aw.BugFix.Tests.Slices;

/// <summary>What a run leaves in the work tree, files a fix adds that a loader picks first, and a judge call that outlives its attempt.</summary>
public sealed class LeftoversAndShadowingTests : IDisposable
{
    private const string SecondWorktree = "/wt-a2";

    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_report_the_run_leaves_behind_does_not_block_recording_or_the_next_run()
    {
        var c1 = await _w.Candidate();
        _w.Runner.WhileRunning = dir => _w.Repos.Write($"{dir}/.coverage", $"coverage {_w.Runner.Runs.Count}\n");

        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Ok(await _w.TestRun(World.Worktree));   // the leftover of a registered run is no stray file
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_leftover_changed_after_its_run_or_a_file_no_run_left_is_refused()
    {
        var c1 = await _w.Candidate();
        _w.Runner.WhileRunning = dir => _w.Repos.Write($"{dir}/.coverage", "run\n");
        _ = _w.Runner.Exits("bash", 0);
        var run = World.Ok(await _w.TestRun(World.Worktree));
        _w.Runner.WhileRunning = null;

        _w.Repos.Write($"{World.Worktree}/.coverage", "edited\n");
        Assert.Contains(".coverage", World.Refused(await RecordRun(c1, run.Evidence), BugFixErrors.DirtyTree).Message, StringComparison.Ordinal);

        _w.Repos.Delete($"{World.Worktree}/.coverage");
        _w.Repos.Write($"{World.Worktree}/argparse.py", "raise SystemExit(0)\n");
        Assert.Contains("argparse.py", World.Refused(await _w.TestRun(World.Worktree), BugFixErrors.DirtyTree).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_report_the_frozen_command_names_is_output_not_a_protected_file()
    {
        string[] command = ["bash", World.Check, "--junitxml=out/r.xml"];
        await _w.Investigated();
        _w.Runner.WhileRunning = dir => _w.Repos.Write($"{dir}/out/r.xml", $"<r n='{_w.Runner.Runs.Count}'/>\n");
        var baseline = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, World.Check, World.Repo, command)));
        _ = World.Ok(await _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, baseline.Evidence, World.Check, World.Repo)));
        Assert.DoesNotContain("out/r.xml", _w.Now.Check?.CommandFiles ?? [], StringComparer.Ordinal);

        _ = World.Ok(await _w.Start());
        _ = _w.Fix();
        var c1 = World.Ok(await _w.Record()).Candidate;
        _ = _w.Runner.Exits("bash", 0);
        var run = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, World.Check, World.Worktree, command)));

        Assert.True(World.Ok(await RecordRun(c1, run.Evidence)).Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task What_a_run_of_another_check_left_behind_is_not_allowed()
    {
        _w.Repos.Write($"{World.Repo}/tools/mk.sh", "planter\n");
        _ = _w.Repos.Commit(World.Repo);
        var c1 = await _w.Candidate();
        _ = _w.Runner.Exits("bash", 0);
        _w.Runner.WhileRunning = dir => _w.Repos.Write($"{dir}/notes.txt", "planted\n");
        _ = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, "tools/mk.sh", World.Worktree, ["bash", "tools/mk.sh"])));
        _w.Runner.WhileRunning = null;

        Assert.Contains("notes.txt", World.Refused(await _w.TestRun(World.Worktree), BugFixErrors.DirtyTree).Message, StringComparison.Ordinal);
        Assert.Null(_w.Now.Candidates.Single(c => c.Id == c1).Run);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("argparse.py")]
    [InlineData("tests/helper.sh")]
    [InlineData("sitecustomize.py")]
    public async Task A_file_that_changes_what_runs_is_never_a_leftover_even_from_the_frozen_check(string written)
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.Exits("bash", 0);
        _w.Runner.WhileRunning = dir => _w.Repos.Write($"{dir}/{written}", "exit 0\n");
        var run = World.Ok(await _w.TestRun(World.Worktree));
        _w.Runner.WhileRunning = null;

        Assert.Contains(written, World.Refused(await RecordRun(c1, run.Evidence), BugFixErrors.DirtyTree).Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("argparse.py")]
    [InlineData("unittest.pyc")]
    [InlineData("helpers/__init__.py")]
    [InlineData("tests/sitecustomize.py")]
    [InlineData("lib/setup.node")]
    [InlineData("node_modules/vitest/index.js")]
    public async Task A_fix_that_adds_a_file_a_loader_picks_first_needs_approval(string added)
    {
        await _w.Investigated();
        _ = _w.Runner.Exits("python3", 1);
        var run = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, World.Check, World.Repo, ["python3", "-m", "unittest", World.Check])));
        _ = World.Ok(await _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, run.Evidence, World.Check, World.Repo)));
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "keep cookie\n", [added] = "x\n" });

        Assert.Contains(added, World.Refused(await _w.Record(), BugFixErrors.CandidateRefused).Message, StringComparison.Ordinal);
        _ = World.Ok(await _w.Record(allowTestChanges: true));   // the user may approve it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_verdict_that_arrives_after_its_candidate_was_left_behind_is_not_recorded()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(JudgeDecision.Escalated));
        _w.Judge.WhileDeciding = () => NextAttemptReachesEvaluate().GetAwaiter().GetResult();

        Assert.Contains("left behind", World.Refused(await Judge(c1), BugFixErrors.JudgeRefused).Message, StringComparison.Ordinal);

        Assert.Equal((2, BugFixStatus.Active), (_w.Now.Attempt, _w.Now.Status));   // the late escalation didn't flip the new attempt
        Assert.Null(_w.Now.Candidates[0].Verdict);
    }

    // The user leaves the interrupted question behind, and attempt 2's candidate gets a passing run.
    private async Task NextAttemptReachesEvaluate()
    {
        _ = World.Ok(await _w.Run<StartAttemptRequest, StartAttemptResponse>(new StartAttemptRequest(World.State, FixMode.A, AbandonJudge: true)));
        _ = _w.Fix("bugfix/eng-1-a2", SecondWorktree, new(StringComparer.Ordinal) { ["src/app.ts"] = "keep the cookie\n" });
        var c2 = World.Ok(await _w.Record("bugfix/eng-1-a2", SecondWorktree)).Candidate;
        var run = World.Ok(await _w.TestRun(SecondWorktree));
        _ = World.Ok(await RecordRun(c2, run.Evidence));
    }

    private Task<Result<RecordRunResponse>> RecordRun(CandidateId candidate, string evidence) =>
        _w.Run<RecordRunRequest, RecordRunResponse>(new RecordRunRequest(World.State, candidate, evidence));

    private Task<Result<JudgeCandidateResponse>> Judge(CandidateId candidate) =>
        _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, candidate));
}
