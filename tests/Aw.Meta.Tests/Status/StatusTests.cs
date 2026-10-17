using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Status;

public sealed class StatusTests
{
    private readonly MetaWorld _world = new();

    private StatusService Service() => new(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Clock);

    private async Task<StatusResponse> Status()
    {
        var result = await Service().Handle(new StatusRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task Receive(string id, string name, string body)
    {
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine(id, name, body));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_meta_has_nothing_to_report()
    {
        await _world.Ready();

        var status = await Status();

        Assert.Empty(status.Obligations);
        Assert.Empty(status.MergeQueue);
        Assert.Empty(status.OwnerQueue);
        Assert.Equal(0, status.Lanes.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reservation_and_the_builders_advice_are_shown()
    {
        await _world.Ready();
        _world.Heavy.Reserve = 1;
        _world.Resources.Resources = new HostResources(64_000_000, 32_000_000, 0, DiskFreeBytes: 200L * 1024 * 1024 * 1024);

        var status = await Status();

        Assert.Equal(new HeavySlots(3, 1), status.Heavy);
        Assert.Equal("builders: 4 per lane (disk 200 GB free, mem 30 GB available)", status.Builders!.Line());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_pull_request_is_queued_with_its_gate_state_until_it_merges()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #190 079c2541");
        var ungated = await Status();
        _ = await _world.Gates.Save(new GateRecord(190, "h", "m", ["repo"], [], GateVerdict.Pass, A.At(2)), CancellationToken.None);
        var gated = await Status();
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abcdef123", A.At(3)), CancellationToken.None);
        var merged = await Status();

        Assert.Equal([new QueuedPr(190, "t1-api", "none")], ungated.MergeQueue);
        Assert.Equal([new QueuedPr(190, "t1-api", "Pass")], gated.MergeQueue);
        Assert.Empty(merged.MergeQueue);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_merged_earlier_stays_out_of_the_queue_after_a_later_merge()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #190 079c2541");
        await Receive("m2", "t1-context", "ready #191 079c2541");
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "abcdef123", A.At(3)), CancellationToken.None);
        _ = await _world.Gates.RecordMerge(new MergeRecord(191, "fedcba987", A.At(4)), CancellationToken.None);

        var status = await Status();

        Assert.Empty(status.MergeQueue);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_watch_saw_merged_or_closed_on_GitHub_is_dropped_from_the_queue()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "ready #190 079c2541");
        await Receive("m2", "t1-context", "ready #191 079c2541");
        await Receive("m3", "t1-ui", "ready #192 079c2541");
        _ = await _world.Values.Set(KvKeys.EndedPrs, "190,191", CancellationToken.None);

        var status = await Status();

        Assert.Equal([192], status.MergeQueue.Select(p => p.Pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task What_is_owed_and_what_waits_for_the_owner_is_listed()
    {
        await _world.Ready();
        await Receive("m1", "t1-api", "OWNER QUESTION: which db?");

        var status = await Status();

        Assert.Equal([(ObligationKind.Reply, "m1")], status.Obligations.Select(o => (o.Kind, o.Key)));
        Assert.Equal("which db?", Assert.Single(status.OwnerQueue).Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_last_health_summary_is_shown_once_health_has_run()
    {
        await _world.Ready();
        var never = await Status();
        _ = await _world.Values.Set(KvKeys.HealthLast, "2026-10-15T05:00:00Z ok: orphans=0", CancellationToken.None);

        var ran = await Status();

        Assert.Null(never.LastHealth);
        Assert.Equal("2026-10-15T05:00:00Z ok: orphans=0", ran.LastHealth);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_it_fails_with_AW_META_002()
    {
        var result = await Service().Handle(new StatusRequest(), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, result.Error.Code);
    }
}
