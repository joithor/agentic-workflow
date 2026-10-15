using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Gate;

public sealed class GateRerunTests
{
    private const string GateTree = "/state/meta/gates/190/tree";
    private const string MainTree = "/state/meta/gates/190/main-tree";
    private const string UnitLog = "/state/meta/gates/190/1/unit.log";
    private readonly MetaWorld _world = new();

    private GateRerunService Service() => new(
        _world.Config,
        _world.Gates,
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths),
        new RerunExecutor(_world.Runner, _world.Paths),
        _world.LogFiles);

    private async Task SaveRedGate()
    {
        await _world.ReadyWithGate();
        _world.LogFiles.Put(UnitLog, "build ok\nfailed Spike.Eval_runs (23ms)\n  Expected: 3\n  Actual: 4\n");
        var steps = new List<GateStep> { new("sync-rules", GateVerdict.Pass, "/l/s.log"), new("unit", GateVerdict.Fail, UnitLog) };
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo", "dotnet"], steps, GateVerdict.Fail, A.At(5)), CancellationToken.None);
    }

    // The test's result on the gate tree and on the main tree.
    private void Script(CommandResult onGate, CommandResult onMain)
    {
        _ = _world.Runner
            .When("--filter-method", request => request.Cwd == MainTree ? onMain : onGate)
            .When("git");
    }

    private static CommandResult Pass() => new(0, "Passed!", string.Empty, TimedOut: false);

    private static CommandResult Fail(string text = "failed Spike.Eval_runs (900ms)\n  Expected: 3\n  Actual: 4") => new(1, text, string.Empty, TimedOut: false);

    private async Task<GateRerunResponse> Rerun(string test = "Eval_runs")
    {
        var result = await Service().Handle(new GateRerunRequest(190, "unit", test), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<FlakeVerdict?> Stored() =>
        (await _world.Gates.Latest(190, CancellationToken.None)).Value!.Steps.First(s => s.Name == "unit").Flake;

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_passes_alone_is_a_flake_and_main_is_not_run()
    {
        await SaveRedGate();
        Script(onGate: Pass(), onMain: Fail());

        var response = await Rerun();

        Assert.Equal((FlakeVerdict.Flake, FlakeVerdict.Flake), (response.Verdict, await Stored()));
        Assert.Equal(1, _world.Runner.Requests.Count(r => r.Argv.Contains("--filter-method", StringComparer.Ordinal)));
        var run = _world.Runner.Requests.Single(r => r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Equal(["/bin/heavy", "dotnet", "test", "--solution", "X.slnx", "--filter-method", "*Eval_runs*"], run.Argv);
        Assert.Equal(GateTree, run.Cwd);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_the_way_it_fails_on_main_is_a_flake()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail("failed Spike.Eval_runs (12ms)\n  Expected: 3\n  Actual: 4"));

        var response = await Rerun();

        Assert.Equal(FlakeVerdict.Flake, response.Verdict);
        Assert.Contains(_world.Runner.Requests, r => r.Cwd == MainTree && r.Argv.Contains("--filter-method", StringComparer.Ordinal));
        Assert.Contains("git checkout --quiet --force --detach mainsha", _world.Runner.Lines, StringComparer.Ordinal);   // the main tree is the main the gate ran on
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_but_passes_on_main_is_real()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Pass());

        var response = await Rerun();

        Assert.Equal((FlakeVerdict.Real, FlakeVerdict.Real), (response.Verdict, await Stored()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_that_fails_alone_differently_from_main_is_real()
    {
        await SaveRedGate();
        Script(onGate: Fail(), onMain: Fail("failed Spike.Eval_runs\n  timeout after 30s\n  nothing else"));

        Assert.Equal(FlakeVerdict.Real, (await Rerun()).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_known_flake_is_a_flake_even_when_it_fails_everywhere_differently()
    {
        await SaveRedGate();
        Script(onGate: Fail("failed SpikeEvalTests.Run\n  boom a"), onMain: Fail("failed SpikeEvalTests.Run\n  boom b"));

        var response = await Rerun("SpikeEvalTests.Run");

        Assert.Equal(FlakeVerdict.Flake, response.Verdict);
        Assert.Contains("known flake", response.Reason, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nope", "unit")]
    [InlineData("sync-rules", "unit")]
    public async Task A_step_that_cannot_be_reran_or_did_not_fail_is_refused(string step, string unused)
    {
        await SaveRedGate();
        Script(onGate: Pass(), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, step, "T" + unused), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.DoesNotContain(_world.Runner.Requests, r => r.Argv.Contains("--filter-method", StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_with_no_gate_is_refused()
    {
        await _world.ReadyWithGate();

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "T"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_test_matching_the_filter_is_refused_not_called_real()
    {
        await SaveRedGate();
        Script(onGate: new CommandResult(8, "Zero tests ran", string.Empty, TimedOut: false), onMain: Pass());

        var result = await Service().Handle(new GateRerunRequest(190, "unit", "Typo"), CancellationToken.None);

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Null(await Stored());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_request_needs_a_pull_request_a_step_and_a_test()
    {
        var validator = new GateRerunValidator();

        Assert.True(validator.Validate(new GateRerunRequest(190, "unit", "T")).IsSuccess);
        Assert.All(
            [new GateRerunRequest(0, "unit", "T"), new GateRerunRequest(1, " ", "T"), new GateRerunRequest(1, "unit", "")],
            request => Assert.Equal(MetaErrors.Invalid.Code, validator.Validate(request).Error.Code));
    }
}
