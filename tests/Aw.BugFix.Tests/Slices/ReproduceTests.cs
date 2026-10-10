using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.RunUi;
using Aw.Kernel;

namespace Aw.BugFix.Tests.Slices;

public sealed class ReproduceTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Run_test_registers_a_record_of_the_run_at_head()
    {
        await _w.Investigated();

        var run = World.Ok(await _w.TestRun());

        Assert.Equal((RunOutcome.Failed, 1, _w.Base), (run.Outcome, run.ExitCode, run.Commit));
        var stored = World.Ok(_w.Ledger.Read(run.Evidence));
        Assert.Equal(World.TestCommand, stored.Record.Command);
        Assert.Equal(World.Ok(_w.Repos.Sha256($"{World.Repo}/{World.Check}")), stored.Record.CheckSha256);
        Assert.Contains(_w.Now.Runs, r => r.Evidence == run.Evidence && r.Sha256 == stored.Sha256);
        Assert.Equal(World.Repo, Assert.Single(_w.Runner.Runs).Directory);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "bash", "-c", "exit 1", "tests/bug.test.sh" }, "inline code (`bash -c`)")]
    [InlineData(new[] { "/usr/bin/grep", "fail", "tests/bug.test.sh" }, "not `grep`")]
    [InlineData(new[] { "python3", "-ce", "1", "tests/bug.test.sh" }, "inline code")]
    [InlineData(new[] { "bash", "tests/other.sh" }, "pass the check file (tests/bug.test.sh) as an argument")]
    public async Task Run_test_refuses_a_command_that_could_pass_without_running_the_check(string[] command, string reason)
    {
        await _w.Investigated();

        var result = await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, World.Check, World.Repo, command));

        Assert.Contains(reason, World.Refused(result, BugFixErrors.CommandRefused).Message, StringComparison.Ordinal);
        Assert.Empty(_w.Runner.Runs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_test_accepts_the_check_named_by_any_path_to_it()
    {
        await _w.Investigated();
        _w.Repos.Link("/repo/link.sh", "/repo/tests/bug.test.sh");

        var result = await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, World.Check, World.Repo, ["bash", "./link.sh"]));

        _ = World.Ok(result);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_test_refuses_a_dirty_tree_and_an_uncommitted_check()
    {
        await _w.Investigated();
        _w.Repos.Write("/repo/notes.txt", "wip");

        _ = World.Refused(await _w.TestRun(), BugFixErrors.DirtyTree);
        _ = _w.Repos.Commit(World.Repo);
        _w.Repos.Write("/repo/tests/new.sh", "x");
        _ = _w.Repos.Commit(World.Repo);
        _w.Repos.Write("/repo/tests/new.sh", "changed");
        var result = await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, "tests/new.sh", World.Repo, ["bash", "tests/new.sh"]));
        _ = World.Refused(result, BugFixErrors.DirtyTree);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_reproduce_freezes_the_check_and_the_baseline()
    {
        await _w.Investigated();
        var run = World.Ok(await _w.TestRun());

        var result = World.Ok(await Reproduce(run.Evidence));

        Assert.Equal((Phase.Reproduce, World.Check, _w.Base), (result.Phase, result.Check, result.BaselineCommit));
        var state = _w.Now;
        Assert.Equal(new Baseline(run.Evidence, _w.Base), state.Baseline);
        Assert.Equal((CheckKind.Test, World.Check), (state.Check?.Kind, state.Check?.Path));
        Assert.Equal(World.TestCommand, state.Check?.Command);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_reproduce_refuses_a_check_that_passes_on_the_unfixed_code()
    {
        await _w.Investigated();
        _ = _w.Runner.Exits("bash", 0);
        var run = World.Ok(await _w.TestRun());

        var error = World.Refused(await Reproduce(run.Evidence), BugFixErrors.NotReproduced);

        Assert.Contains("the bug is not reproduced", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_reproduce_refuses_evidence_it_did_not_write_or_that_was_edited()
    {
        await _w.Investigated();
        var run = World.Ok(await _w.TestRun());
        var record = World.Ok(_w.Ledger.Read(run.Evidence)).Record;
        _w.Ledger.Forge("/state/eng-1/runs/forged.json", record);
        _w.Ledger.Tamper(run.Evidence, record with { Outcome = RunOutcome.Failed, ExitCode = 2 });

        _ = World.Refused(await Reproduce("/state/eng-1/runs/forged.json"), BugFixErrors.EvidenceRefused);
        _ = World.Refused(await Reproduce(run.Evidence), BugFixErrors.EvidenceRefused);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_reproduce_refuses_a_run_from_another_commit()
    {
        await _w.Investigated();
        var run = World.Ok(await _w.TestRun());
        _w.Repos.Write("/repo/src/app.ts", "moved on\n");
        _ = _w.Repos.Commit(World.Repo);

        _ = World.Refused(await Reproduce(run.Evidence), BugFixErrors.NotReproduced);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_ui_registers_the_script_run_and_a_broken_baseline_is_not_a_reproduction()
    {
        await _w.Investigated();
        _w.Repos.Write("/repo/.ui-evidence/eng-1.json", "{}");
        _ = _w.Repos.Commit(World.Repo);
        _ = _w.Ui.Steps(RunOutcome.Passed, RunOutcome.Broken);

        var run = World.Ok(await _w.Run<RunUiRequest, RunUiResponse>(new RunUiRequest(World.State, ".ui-evidence/eng-1.json", World.Repo)));

        Assert.Equal(RunOutcome.Broken, run.Outcome);
        Assert.Equal(World.Ok(_w.Repos.Head(World.Repo)), Assert.Single(_w.Ui.Requests).Commit);
        var error = World.Refused(await Reproduce(run.Evidence, ".ui-evidence/eng-1.json"), BugFixErrors.NotReproduced);
        Assert.Contains("broken selector", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Run_ui_registers_nothing_when_ui_evidence_produces_no_run()
    {
        await _w.Investigated();
        _w.Repos.Write("/repo/.ui-evidence/eng-1.json", "{}");
        _ = _w.Repos.Commit(World.Repo);
        _ = _w.Ui.Fails(BugFixErrors.UiEvidenceUnavailable.With("ui-evidence wrote no summary"));

        _ = World.Refused(await _w.Run<RunUiRequest, RunUiResponse>(new RunUiRequest(World.State, ".ui-evidence/eng-1.json", World.Repo)), BugFixErrors.UiEvidenceUnavailable);
        Assert.Empty(_w.Now.Runs);
    }

    private Task<Result<AdvanceReproduceResponse>> Reproduce(string evidence, string check = World.Check) =>
        _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, evidence, check, World.Repo));
}
