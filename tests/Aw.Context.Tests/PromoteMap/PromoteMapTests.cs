using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.PromoteMap;

public sealed class PromoteMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task Decide(ReviewDecision decision) => Assert.True((await _world.Map.ApplyReview(decision, Drafts.Scope, MapWorld.None)).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public async Task Promoting_with_no_draft_is_SND_CONTEXT_046_and_names_the_fix()
    {
        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
        Assert.Contains("fix:", promoted.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_with_undecided_items_is_SND_CONTEXT_046_and_nothing_changes()
    {
        var draft = (await Drafts.Build(_world)).Draft;
        var queue = await Drafts.Queue(_world);

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
        Assert.Contains(queue.Undecided.ToString(CultureInfo.InvariantCulture) + " undecided", promoted.Error.Message, StringComparison.Ordinal);
        Assert.Equal([(draft.Number, MapVersionStatus.Draft)], (await _world.Store.Versions(MapWorld.None)).Value.Select(v => (v.Number, v.Status)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_that_finds_new_model_items_needs_them_decided_before_it_can_be_promoted()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));
        _ = await Drafts.Build(_world, clusters: true);   // the rebuild keeps the decisions; the model items are new
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = await _world.Map.Promote(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, promoted.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Once_every_item_is_decided_promote_makes_the_draft_current_and_retains_the_old_one()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));
        var first = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;
        var second = (await Drafts.Build(_world)).Draft;
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal(MapVersionStatus.Current, first.Version.Status);
        Assert.Null(first.Retained);
        Assert.Equal((second.Number, MapVersionStatus.Current), (promoted.Version.Number, promoted.Version.Status));
        Assert.Equal((first.Version.Number, MapVersionStatus.Retained), (promoted.Retained!.Number, promoted.Retained.Status));
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        _ = Assert.Single(versions, v => v.Status == MapVersionStatus.Current);
        Assert.DoesNotContain(versions, v => v.Status == MapVersionStatus.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promoted_map_is_what_status_and_queries_read()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = (await _world.Map.Promote(Drafts.Scope, MapWorld.None)).Value;

        var status = (await _world.Map.MapStatus(anchor: null, Drafts.Scope, MapWorld.None)).Value;
        Assert.Equal(promoted.Version.Number, status.Version.Number);
        Assert.True(status.Counts.Nodes > 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_cant_promote()
    {
        _ = await Drafts.Build(_world);
        await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic));

        var promoted = await _world.Map.Promote(MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, promoted.Error.Code);
        _ = Assert.Single((await _world.Store.Versions(MapWorld.None)).Value, v => v.Status == MapVersionStatus.Draft);
    }
}
