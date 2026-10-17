using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Reserve;

public sealed class HeavyReservationTests
{
    private readonly MetaWorld _world = new();

    private async Task Ready(int depth)
    {
        await _world.ReadyWithGate();
        for (var n = 0; n < depth; n++)
        {
            _world.Transcripts.Append(MetaWorld.Transcript, MetaWorld.PeerLine("m" + n.ToString(CultureInfo.InvariantCulture), "t1-api", string.Create(CultureInfo.InvariantCulture, $"ready #{190 + n} 079c2541")));
            _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
        }
    }

    private async Task<Kernel.Result<Kernel.Unit>> Apply() =>
        await Worlds.Reservation(_world).Apply((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None);

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    [InlineData(7, 3)]
    public async Task The_reservation_follows_the_merge_queue_depth(int depth, int reserved)
    {
        await Ready(depth);

        var applied = await Apply();

        Assert.True(applied.IsSuccess);
        Assert.Equal(reserved, _world.Heavy.Reserve);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task All_means_the_cap_file_value()
    {
        await Ready(7);
        _world.Heavy.Cap = 5;

        _ = await Apply();

        Assert.Equal(5, _world.Heavy.Reserve);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_change_is_one_NOTE_line_and_an_unchanged_value_logs_and_writes_nothing()
    {
        await Ready(3);

        _ = await Apply();
        _ = await Apply();

        Assert.Equal(1, _world.Heavy.Writes);
        Assert.Equal("- NOTE 2026-10-15 05:00: heavy reserve 0 -> 1 of 3 slots (merge queue depth 3)", Assert.Single(_world.Log.Lines));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merged_ended_and_dropped_pull_requests_do_not_count()
    {
        await Ready(4);
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abc1234", _world.Clock.GetUtcNow()), CancellationToken.None);
        _ = await _world.Values.Set(KvKeys.EndedPrs, "191", CancellationToken.None);

        _ = await Apply();

        Assert.Equal(0, _world.Heavy.Reserve);   // 192 and 193 remain: depth 2
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_that_turns_the_reservation_off_never_has_the_file_touched()
    {
        await Ready(7);
        var team = (await _world.Config.Load(CancellationToken.None)).Value with { Heavy = new HeavyConfig(ReserveSetting.Off) };
        _ = await _world.Config.Save(team, CancellationToken.None);
        _world.Heavy.Reserve = 2;

        _ = await Apply();

        Assert.Equal((2, 0), (_world.Heavy.Reserve, _world.Heavy.Writes));
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Custom_tiers_replace_the_defaults()
    {
        await Ready(2);
        var team = (await _world.Config.Load(CancellationToken.None)).Value with { Heavy = new HeavyConfig(new ReserveSetting(Enabled: true, [new ReserveTier(2, ReserveSlots.All)])) };
        _ = await _world.Config.Save(team, CancellationToken.None);

        _ = await Apply();

        Assert.Equal(3, _world.Heavy.Reserve);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_run_sets_the_reservation_before_its_steps_run()
    {
        await Ready(3);
        var reserveWhenStepsRan = new List<int>();
        ScriptGate(() => reserveWhenStepsRan.Add(_world.Heavy.Reserve));

        var gate = new GateRunService(_world.Config, new GateWorkspace(_world.Repo, _world.Trees, _world.Paths), new GateStepRunner(_world.Runner, _world.Paths), _world.Gates, Worlds.Reservation(_world), _world.Clock);
        var result = await gate.Handle(new GateRunRequest(190, ["dotnet"]), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.NotEmpty(reserveWhenStepsRan);
        Assert.All(reserveWhenStepsRan, reserved => Assert.Equal(1, reserved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_run_goes_ahead_when_the_reservation_cannot_be_written()
    {
        await _world.ReadyWithGate();
        ScriptGate();
        _world.Heavy.FailWrites = true;
        var gate = new GateRunService(_world.Config, new GateWorkspace(_world.Repo, _world.Trees, _world.Paths), new GateStepRunner(_world.Runner, _world.Paths), _world.Gates, Worlds.Reservation(_world), _world.Clock);

        var result = await gate.Handle(new GateRunRequest(190, ["repo"]), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merging_the_last_queued_pull_request_releases_the_reservation()
    {
        await Ready(3);
        _ = await Apply();
        Assert.Equal(1, _world.Heavy.Reserve);
        foreach (var pr in new[] { 191, 192 })
        {
            _ = await _world.Gates.RecordMerge(new MergeRecord(pr, "abc1234", _world.Clock.GetUtcNow()), CancellationToken.None);
        }

        _ = await _world.Gates.Save(new Core.Gate.GateRecord(190, "headsha", "mainsha", ["repo"], [new Core.Gate.GateStep("sync-rules", Core.Gate.GateVerdict.Pass, "/l/sync.log")], Core.Gate.GateVerdict.Pass, _world.Clock.GetUtcNow()), CancellationToken.None);
        var merged = false;
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"headsha","isDraft":false,"title":"Add x","state":"OPEN"}""")
            .When("gh pr merge 190 --merge", _ => { merged = true; return new Core.Commands.CommandResult(0, string.Empty, string.Empty, TimedOut: false); })
            .When("rev-parse --verify --quiet origin/main", _ => new Core.Commands.CommandResult(0, merged ? "newmainsha1234\n" : "mainsha\n", string.Empty, TimedOut: false))
            .When("git diff --name-only mainsha...headsha", "README.md\n")
            .When("git fetch");
        var merge = new MergeRunService(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles), _world.Repo, _world.Gates, _world.Log, Worlds.Reservation(_world), _world.Clock);

        var result = await merge.Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess && result.Value.Merged, result.IsError ? result.Error.Message : "refused");
        Assert.Equal(0, _world.Heavy.Reserve);
        Assert.Contains(_world.Log.Lines, l => l.Contains("heavy reserve 1 -> 0 of 3 slots (merge queue depth 0)", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_gate_step_runs_with_meta_priority()
    {
        await _world.ReadyWithGate();
        ScriptGate();
        var gate = new GateRunService(_world.Config, new GateWorkspace(_world.Repo, _world.Trees, _world.Paths), new GateStepRunner(_world.Runner, _world.Paths), _world.Gates, Worlds.Reservation(_world), _world.Clock);

        _ = await gate.Handle(new GateRunRequest(190, []), CancellationToken.None);

        var steps = _world.Runner.Requests.Where(r => r.LogFile is not null).ToList();
        Assert.Equal(3, steps.Count);
        Assert.All(steps, step => Assert.Equal("meta", step.Env["AW_HEAVY_PRIORITY"]));
    }

    private void ScriptGate(Action? onStep = null) =>
        _ = _world.Runner
            .When("gh pr view 190", """{"headRefOid":"headsha","headRefName":"feat/x","isDraft":false,"title":"Add x"}""")
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git diff --name-only", "src/a.cs\n")
            .When("git")
            .When("/bin/heavy", _ => { onStep?.Invoke(); return new Core.Commands.CommandResult(0, string.Empty, string.Empty, TimedOut: false); })
            .When("scripts/sync-rules.sh");
}
