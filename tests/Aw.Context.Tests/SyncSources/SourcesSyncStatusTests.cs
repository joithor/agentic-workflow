using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
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
}
