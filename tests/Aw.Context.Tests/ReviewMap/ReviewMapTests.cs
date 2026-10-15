using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ReviewMap;

public sealed class ReviewMapTests : IDisposable
{
    private static readonly NodeKey _split = NodeKey.From("surface:command/split-vertical");
    private static readonly NodeKey _close = NodeKey.From("surface:command/close-pane");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<Aw.Kernel.Result<ReviewResult>> Decide(ReviewDecision decision, ContextScope? scope = null) =>
        await _world.Map.ApplyReview(decision, scope ?? Drafts.Scope, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task The_queue_lists_the_deterministic_proposals_first_then_the_model_ones_with_their_files()
    {
        var draft = (await Drafts.Build(_world, clusters: true)).Draft;

        var queue = await Drafts.Queue(_world);

        Assert.Equal(Drafts.Label(draft), queue.Draft);
        Assert.Equal(0, queue.Decided);
        var groups = queue.Entries.Select(e => e.Group).ToList();
        Assert.Equal(groups.Order(), groups);
        Assert.Contains(queue.Entries, e => e.Node.Value == _split.Value && e.Group == ReviewGroup.Deterministic && e.Files.Select(f => f.Value).SequenceEqual(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], StringComparer.Ordinal));
        Assert.Contains(queue.Entries, e => e.Group == ReviewGroup.Model && e.Link is null && e.Node.Kind == NodeKind.Feature);
        Assert.Equal(2, queue.Entries.Count(e => e.Link is not null));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_draft_the_queue_is_SND_CONTEXT_044()
    {
        _ = await _world.ImportFixture();

        var queue = await _world.Map.ReviewQueue(Drafts.Scope, MapWorld.None);

        Assert.Equal(ContextErrors.NoDraftMap.Code, queue.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_cant_read_the_queue_or_decide()
    {
        _ = await Drafts.Build(_world);
        var session = MapWorld.Session(FixtureMap.Repo);

        var queue = await _world.Map.ReviewQueue(session, MapWorld.None);
        var decision = await Decide(new ReviewDecision(ReviewVerb.Accept, _split), session);

        Assert.Equal((ContextErrors.SessionsDisabled.Code, ContextErrors.SessionsDisabled.Code), (queue.Error.Code, decision.Error.Code));
        Assert.Equal(0, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepting_a_node_writes_exactly_one_row_and_takes_it_out_of_the_queue()
    {
        _ = await Drafts.Build(_world);
        var before = await Drafts.Queue(_world);

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.Equal((OverrideAction.Accept, _split.Value, Environment.UserName), (row.Action, row.Target.Value, row.Who));
        Assert.Equal([row.ToId().Value], result.Written.Select(w => w.Value), StringComparer.Ordinal);
        Assert.Equal(before.Undecided - 1, result.Undecided);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _split.Value);
        Assert.Equal(1, (await Drafts.Queue(_world)).Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rejecting_a_node_records_what_was_rejected_so_a_rebuild_does_not_bring_it_back()
    {
        _ = await Drafts.Build(_world);

        _ = (await Decide(new ReviewDecision(ReviewVerb.Reject, _close))).Value;
        _ = await Drafts.Build(_world);

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.NotNull(row.Rejection);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _close.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Accepting_every_deterministic_proposal_writes_one_row_per_entry_and_leaves_the_model_ones()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var before = await Drafts.Queue(_world);
        var deterministic = before.Entries.Count(e => e.Group == ReviewGroup.Deterministic);

        var result = (await Decide(new ReviewDecision(ReviewVerb.AcceptDeterministic))).Value;

        Assert.True(deterministic >= 4);
        Assert.Equal(deterministic, result.Written.Count);
        Assert.Equal(deterministic, await Drafts.Rows(_world));
        Assert.Equal(before.Undecided - deterministic, result.Undecided);
        Assert.All((await Drafts.Queue(_world)).Entries, e => Assert.Equal(ReviewGroup.Model, e.Group));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggested_link_is_accepted_by_its_id_in_one_row()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var link = (await Drafts.Queue(_world)).Entries.First(e => e.Link is not null);

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link.Link))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value);
        Assert.Equal(link.Link, row.Link);
        _ = Assert.Single(result.Written);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Link == link.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rename_and_move_are_one_row_each()
    {
        _ = await Drafts.Build(_world, clusters: true);
        var feature = (await Drafts.Queue(_world)).Entries.First(e => e.Group == ReviewGroup.Model && e.Link is null).Node;

        _ = (await Decide(new ReviewDecision(ReviewVerb.Rename, _split, Label: "Split the pane vertically"))).Value;
        _ = (await Decide(new ReviewDecision(ReviewVerb.Move, _close, Other: feature))).Value;

        var rows = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Split the pane vertically", rows.Single(r => r.Action == OverrideAction.Rename).Label);
        Assert.Equal(feature.Value, rows.Single(r => r.Action == OverrideAction.Move).Other!.Value.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decided_or_unknown_target_is_SND_CONTEXT_047_and_writes_nothing()
    {
        _ = await Drafts.Build(_world);
        _ = await Decide(new ReviewDecision(ReviewVerb.Accept, _split));

        var again = await Decide(new ReviewDecision(ReviewVerb.Accept, _split));
        var unknown = await Decide(new ReviewDecision(ReviewVerb.Reject, NodeKey.From("surface:command/never-found")));
        var linkGone = await Decide(new ReviewDecision(ReviewVerb.Accept, Link: LinkId.From("edge:0123456789abcdef")));

        Assert.All([again, unknown, linkGone], r => Assert.Equal(ContextErrors.ReviewTargetStale.Code, r.Error.Code));
        Assert.Equal(1, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undo_reverses_one_decision_with_a_row_of_its_own_and_the_item_is_back_in_the_queue()
    {
        _ = await Drafts.Build(_world);
        var accepted = (await Decide(new ReviewDecision(ReviewVerb.Accept, _split))).Value.Written[0];

        var undone = (await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: accepted))).Value;
        var twice = await Decide(new ReviewDecision(ReviewVerb.Undo, Undoes: accepted));

        Assert.Equal(2, await Drafts.Rows(_world));
        _ = Assert.Single(undone.Written);
        Assert.Contains((await Drafts.Queue(_world)).Entries, e => e.Node.Value == _split.Value);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, twice.Error.Code);
        Assert.Equal(2, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_orphaned_override_is_dropped_with_an_undo_row()
    {
        _ = await Drafts.Build(_world);
        var orphan = new MapOverride(OverrideAction.Accept, NodeKey.From("surface:command/long-gone"), "avery", DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _ = await _world.Store.AppendOverrides([orphan], MapWorld.None);
        var entry = Assert.Single((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);

        var dropped = (await Decide(new ReviewDecision(ReviewVerb.Drop, Undoes: entry.Override))).Value;

        _ = Assert.Single(dropped.Written);
        Assert.DoesNotContain((await Drafts.Queue(_world)).Entries, e => e.Group == ReviewGroup.Orphaned);
        Assert.Equal(2, await Drafts.Rows(_world));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_link_in_the_current_map_is_decided_when_there_is_no_draft()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var link = LinkIds.For(new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements));

        var result = (await Decide(new ReviewDecision(ReviewVerb.Accept, Link: link))).Value;

        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Link is not null);
        Assert.Equal((link, 1), (row.Link!.Value, result.Written.Count));
    }
}
