using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.SyncSources;

/// <summary>'context sources status' for an intent source (S-5B, spec §8.8): when it last synced, and what the sync holds.</summary>
[Trait("Lane", "integration")]
public sealed class SourcesSyncStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SourceStatusRow> Onyx(Rig rig) =>
        (await rig.World.Engine.Sources(Scope(), Ct)).Value.Sources.Single(r => r.Source.Value == "onyx");

    [Fact]
    public async Task SourcesStatus_shows_last_sync_links_decisions_queued_per_intent_source()
    {
        await using var rig = await Rig.Start();
        var before = (await Onyx(rig)).Sync;

        _ = await rig.Sync(Scope());
        var after = (await Onyx(rig)).Sync;

        Assert.Equal((null, SourceState.Degraded, 0), (before?.LastSync, before?.State, before?.Records));   // never synced
        Assert.NotNull(after);
        var layer = await rig.Layer();
        Assert.Equal((SourceState.Ok, layer.Records.Count, layer.Links.Count, layer.Decisions.Count, 0, null),
            (after.State, after.Records, after.Linked, after.Decisions, after.Queued, after.Code));
        Assert.True(after.LastSync is not null);
        Assert.True(after.Decisions > 0);
    }

    [Fact]
    public async Task While_labeling_is_off_and_records_are_queued_status_shows_042_after_every_run_and_the_source_stays_ok()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));
        var replay = await rig.Sync(Scope(labeling: false));   // fetches nothing, so its own event carries no 042

        var sync = (await Onyx(rig)).Sync;

        Assert.True(replay.Queued > 0);
        Assert.NotNull(sync);
        Assert.Equal((SourceState.Ok, ContextErrors.LabelingUnavailable.Code), (sync.State, sync.Code));
        Assert.True(sync.Queued > 0);
    }

    [Fact]
    public async Task Status_drops_042_once_the_queue_is_extracted()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));
        _ = await rig.Sync(Scope());

        var sync = (await Onyx(rig)).Sync;

        Assert.Equal((0, null), (sync?.Queued, sync?.Code));
    }

    [Fact]
    public async Task A_failed_last_sync_is_unavailable_with_its_code_and_a_docs_row_has_no_sync()
    {
        var rig = await Rig.Start();
        await using var disposing = rig;
        _ = await rig.Sync(Scope());
        await rig.Onyx.DisposeAsync();
        _ = await rig.Sync(Scope());

        var view = (await rig.World.Engine.Sources(Scope(), Ct)).Value;

        var sync = view.Sources.Single(r => r.Source.Value == "onyx").Sync;
        Assert.Equal((SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), (sync?.State, sync?.Code));
        Assert.All(view.Sources.Where(r => r.Source.Value == "repo-docs"), r => Assert.Null(r.Sync));
    }

    [Fact]
    public async Task A_run_cut_short_by_the_time_cap_shows_059_and_stays_ok_and_042_stays_for_labeling()
    {
        ScriptedSource? slow = null;
        await using var rig = await Rig.Start(wrap: inner => slow = new ScriptedSource(inner));
        slow!.AfterFetch = () => rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));

        var capped = await rig.Sync(Scope());
        var cut = (await Onyx(rig)).Sync;
        slow.AfterFetch = null;
        _ = await rig.Sync(Scope());
        var after = (await Onyx(rig)).Sync;

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);
        Assert.Equal((SourceState.Ok, ContextErrors.SyncCapped.Code), (cut?.State, cut?.Code));   // partial, not failed: the next run continues
        Assert.Equal((SourceState.Ok, null), (after?.State, after?.Code));
    }

    [Fact]
    public async Task A_model_failure_in_a_run_the_time_cap_also_stopped_shows_the_failure_not_059()
    {
        ScriptedSource? slow = null;
        await using var rig = await Rig.Start(wrap: inner => slow = new ScriptedSource(inner));
        slow!.AfterFetch = () => rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));
        rig.World.Model.Fail = _ => ContextErrors.SourceUnavailable.With("the model is down");

        var capped = await rig.Sync(Scope());
        var sync = (await Onyx(rig)).Sync;

        Assert.Equal(ContextErrors.SourceUnavailable.Code, capped.Code);
        Assert.Equal((SourceState.Unavailable, ContextErrors.SourceUnavailable.Code), (sync?.State, sync?.Code));   // the outage, not "ok / 059, the next run continues"
    }

    [Fact]
    public async Task A_capped_run_with_labeling_off_shows_059_not_042()
    {
        ScriptedSource? slow = null;
        await using var rig = await Rig.Start(wrap: inner => slow = new ScriptedSource(inner));
        slow!.AfterFetch = () => rig.World.Clock.Advance(SyncSourcesService.RunCap + TimeSpan.FromSeconds(1));

        var capped = await rig.Sync(Scope(labeling: false));
        var sync = (await Onyx(rig)).Sync;

        Assert.Equal(ContextErrors.SyncCapped.Code, capped.Code);   // the walk was cut short, and the records held back for labeling do not hide it
        Assert.Equal((SourceState.Ok, ContextErrors.SyncCapped.Code), (sync?.State, sync?.Code));
    }

    [Fact]
    public async Task A_failed_read_of_the_linked_records_fails_the_run_with_its_code_and_fetches_nothing()
    {
        await using var rig = await Rig.Start();
        rig.World.Intents.LinkedUnderFailure = ContextErrors.StoreUnavailable.With("the linked read failed");

        var row = await rig.Sync(Scope());

        Assert.Equal(ContextErrors.StoreUnavailable.Code, row.Code);
        Assert.Equal(0, row.Fetched);   // not "nothing linked yet": that would fetch and re-extract every record
    }
}
