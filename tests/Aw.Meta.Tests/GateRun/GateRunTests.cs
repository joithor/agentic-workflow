using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.GateRun;

public sealed class GateRunTests
{
    private const string Tree = "/state/meta/gates/190/tree";
    private readonly MetaWorld _world = new();

    private GateRunService Service() => new(
        _world.Config,
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths),
        new GateStepRunner(_world.Runner, _world.Paths),
        _world.Gates,
        Worlds.Reservation(_world),
        _world.Clock);

    private void Script(string changed = "src/a.cs\n", int unitExit = 1)
    {
        _ = _world.Runner
            .When("gh pr view 190", """{"headRefOid":"headsha","headRefName":"feat/x","isDraft":false,"title":"Add x"}""")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git diff --name-only origin/main...headsha", changed)
            .When("/bin/heavy dotnet test", exitCode: unitExit)
            .When("git")
            .When("/bin/heavy dotnet build")
            .When("scripts/sync-rules.sh");
    }

    private async Task<GateRunResponse> Run(params string[] scopes)
    {
        var result = await Service().Handle(new GateRunRequest(190, scopes), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gate_merges_the_head_onto_main_in_its_tree_and_runs_the_planned_scopes()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run();

        Assert.Equal(("headsha", "mainsha", GateVerdict.Fail), (response.Gate.Head, response.Gate.Main, response.Gate.Verdict));
        Assert.Equal(["repo", "dotnet"], response.Gate.Scopes);
        Assert.Equal(
            [("sync-rules", GateVerdict.Pass), ("build", GateVerdict.Pass), ("unit", GateVerdict.Fail)],
            response.Gate.Steps.Select(s => (s.Name, s.Verdict)));
        Assert.Contains("git fetch --quiet origin pull/190/head", _world.Runner.Lines, StringComparer.Ordinal);
        Assert.Contains(_world.Runner.Lines, l => l.EndsWith("merge --quiet --no-edit headsha", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_step_runs_in_the_tree_logs_under_the_run_and_heavy_ones_go_through_the_wrapper()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run();

        var steps = _world.Runner.Requests.Where(r => r.LogFile is not null).ToList();
        Assert.Equal(
            [["scripts/sync-rules.sh", "--check"], ["/bin/heavy", "dotnet", "build", "X.slnx"], ["/bin/heavy", "dotnet", "test", "--solution", "X.slnx"]],
            steps.Select(r => r.Argv));
        Assert.All(steps, r => Assert.Equal(Tree, r.Cwd));
        Assert.Equal(
            ["/state/meta/gates/190/1/sync-rules.log", "/state/meta/gates/190/1/build.log", "/state/meta/gates/190/1/unit.log"],
            response.Gate.Steps.Select(s => s.Log),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_passing_gate_is_recorded_and_a_second_run_gets_the_next_number()
    {
        await _world.ReadyWithGate();
        Script(unitExit: 0);

        var first = await Run();
        var second = await Run();

        Assert.Equal(GateVerdict.Pass, first.Gate.Verdict);
        Assert.Equal((2, "/state/meta/gates/190/2/unit.log"), (second.Gate.Run, second.Gate.Steps[2].Log));
        Assert.Equal(2, (await _world.Gates.Latest(190, CancellationToken.None)).Value!.Run);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scopes_named_on_the_command_line_replace_the_plan()
    {
        await _world.ReadyWithGate();
        Script();

        var response = await Run("repo");

        Assert.Equal(["repo"], response.Gate.Scopes);
        Assert.Equal(["sync-rules"], response.Gate.Steps.Select(s => s.Name), StringComparer.Ordinal);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("diff --name-only", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_docs_only_change_runs_only_the_always_scopes()
    {
        await _world.ReadyWithGate();
        Script(changed: "docs/a.md\nREADME.md\n");

        var response = await Run();

        Assert.Equal(["repo"], response.Gate.Scopes);
        Assert.Equal(GateVerdict.Pass, response.Gate.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_head_that_conflicts_with_main_is_recorded_as_a_conflict_with_no_steps()
    {
        await _world.ReadyWithGate();
        _ = _world.Runner.When("merge --quiet", exitCode: 1).When("--diff-filter=U", "a.cs\n");
        Script();

        var response = await Run();

        Assert.Equal((GateVerdict.Conflict, 0), (response.Gate.Verdict, response.Gate.Steps.Count));
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("sync-rules", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_the_config_does_not_have_is_refused_before_anything_runs()
    {
        await _world.ReadyWithGate();
        Script();

        var result = await Service().Handle(new GateRunRequest(190, ["nope"]), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(_world.Runner.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_git_call_is_an_error_and_records_no_gate()
    {
        await _world.ReadyWithGate();
        _ = _world.Runner.When("gh pr view", exitCode: 1);

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new GateRunValidator().Validate(new GateRunRequest(0, [])).Error.Code);
}
