using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ScopeGraph;

/// <summary>The scoping graph the engine builds from the map (issue 1005): anchors, depth, edges inside the set, the cap, and a path only on code nodes.</summary>
public sealed class ScopeGraphTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Groups = "feature:tabs.tab-groups";
    private const string SwitchTab = "surface:command/switch-tab";
    private const string Restore = "action:tab-manager.restore-layout";
    private const string PaneLayout = "thing:pane-layout";
    private const string ManagerCode = "code:src/tabs/TabManager.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task<ScopeGraphView> Build(string[] anchors, int depth, int maxNodes = 400, int maxBytes = 300_000)
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var answer = await _world.Engine.ScopeGraph(new ScopeGraphQuery(anchors, MapRead.Current, new LensRange(depth, null), Brief: null, maxNodes, maxBytes), MapWorld.Human, MapWorld.None);
        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.Message : null);
        return answer.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_starts_at_the_anchor_and_widens_to_the_depth()
    {
        var one = await Build([Manager], depth: 1);
        var three = await Build([Manager], depth: 3);

        Assert.Equal([Manager], one.Anchors, StringComparer.Ordinal);
        Assert.Contains(one.Nodes, n => n.Id == Groups);
        Assert.Contains(one.Nodes, n => n.Id == SwitchTab);
        Assert.DoesNotContain(one.Nodes, n => n.Id == PaneLayout);
        Assert.Contains(three.Nodes, n => n.Id == PaneLayout);
        Assert.Equal((1, 3), (one.Depth, three.Depth));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_resolves_an_anchor_given_by_its_label()
    {
        var view = await Build(["Tab manager"], depth: 1);

        Assert.Equal([Manager], view.Anchors, StringComparer.Ordinal);
        Assert.Contains(view.Nodes, n => n.Id == Manager);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_keeps_only_edges_with_both_ends_in_the_node_set()
    {
        var view = await Build([Manager], depth: 1);

        var ids = view.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(view.Edges);
        Assert.All(view.Edges, e => Assert.True(ids.Contains(e.From) && ids.Contains(e.To), $"{e.From} {e.Type} {e.To} has an end outside the graph"));
        Assert.DoesNotContain(view.Edges, e => e.From == Restore);   // the action is two hops out, so its edges are not in a depth-1 graph
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_puts_a_path_only_on_code_nodes()
    {
        var view = await Build([Manager], depth: 2);

        var code = Assert.Single(view.Nodes, n => n.Id == ManagerCode);
        Assert.Equal("src/tabs/TabManager.ts", code.Path);
        Assert.All(view.Nodes.Where(n => !n.Id.StartsWith("code:", StringComparison.Ordinal)), n => Assert.Null(n.Path));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_summary_is_the_node_label_and_the_anchor_ranks_first_in_the_own_band()
    {
        var view = await Build([Manager], depth: 2);

        var anchor = Assert.Single(view.Nodes, n => n.Id == Manager);
        Assert.Equal(("Tab manager", 1, "own"), (anchor.Summary, anchor.Rank, anchor.Band));
        Assert.Equal(view.Nodes.Select(n => n.Rank).Order(), view.Nodes.Select(n => n.Rank));   // best rank first
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_ranks_product_nodes_before_code_nodes()
    {
        var view = await Build([Manager], depth: 3);

        var worstProduct = view.Nodes.Where(n => !n.Id.StartsWith("code:", StringComparison.Ordinal)).Max(n => n.Rank);
        var bestCode = view.Nodes.Where(n => n.Id.StartsWith("code:", StringComparison.Ordinal)).Min(n => n.Rank);
        Assert.True(worstProduct < bestCode, $"product ranks end at {worstProduct} but code starts at {bestCode}");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_drops_the_lowest_rank_over_max_nodes_and_records_a_cap_limit()
    {
        var all = await Build([Manager], depth: 3);
        var capped = await Build([Manager], depth: 3, maxNodes: 3);

        Assert.True(all.Nodes.Count > 3);
        Assert.Equal(all.Nodes.Take(3).Select(n => n.Id), capped.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        Assert.Contains(capped.Limits, l => l.Why == "cap");
        var ids = capped.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(capped.Edges, e => Assert.True(ids.Contains(e.From) && ids.Contains(e.To)));
        Assert.DoesNotContain(all.Limits, l => l.Why == "cap");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ScopeGraph_drops_the_lowest_rank_over_max_bytes_and_records_a_cap_limit()
    {
        var all = await Build([Manager], depth: 3);
        var small = await Build([Manager], depth: 3, maxBytes: 1_500);

        Assert.True(small.Nodes.Count < all.Nodes.Count);
        Assert.Contains(small.Limits, l => l.Why == "cap");
        Assert.Contains(small.Nodes, n => n.Id == Manager);   // the anchor is the last to go
    }

    [Fact(Skip = "A concept node needs the concept kind from the concepts lane; write this against its map shape once it lands"), Trait("Lane", "unit")]
    public Task ScopeGraph_expands_a_concept_anchor_to_its_member_features_and_summarizes_it_with_its_summary_attribute() =>
        throw new NotSupportedException("waits on the concepts lane");
}
