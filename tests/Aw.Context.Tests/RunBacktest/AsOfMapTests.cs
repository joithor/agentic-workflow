using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Features.RunBacktest;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.RunBacktest;

public sealed class AsOfMapTests
{
    private static async Task<CorpusSample> SampleOf(BacktestWorld world, string key)
    {
        var report = (await world.Run("graph")).Value.Report;
        return report.Samples.Single(s => s.IssueKey == key);
    }

    private static async Task<AsOfGraph> Graph(BacktestWorld world, CorpusSample sample)
    {
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        return (await builder.Build(new AsOfRequest(repo, sample), TestContext.Current.CancellationToken)).Value;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_graph_arm_uses_as_of_map_and_never_a_later_node()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        foreach (var key in new[] { "SPL-102", "SPL-104", "SPL-105" })
        {
            var graph = (await Graph(world, await SampleOf(world, key))).Graph;
            // tabs.tab-manager arrives in PR #4 (merged 07-22): no cut before that has it, however the authored map names it
            Assert.DoesNotContain(graph.Nodes, n => string.Equals(n.Key.Value, "feature:tabs.tab-manager", StringComparison.Ordinal) || string.Equals(n.Key.Value, "code:src/tabs/TabManager.ts", StringComparison.Ordinal) || string.Equals(n.Key.Value, "action:tab-manager.restore-layout", StringComparison.Ordinal));
            Assert.DoesNotContain(graph.Edges, e => e.From.Value == "code:src/tabs/TabManager.ts" || e.To.Value == "feature:tabs.tab-manager");
        }

        var early = (await Graph(world, await SampleOf(world, "SPL-102"))).Graph;   // cut at the initial commit: SplitPane.ts does not exist yet either
        var later = (await Graph(world, await SampleOf(world, "SPL-105"))).Graph;   // cut after merge #3
        Assert.DoesNotContain(early.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.Contains(later.Nodes, n => n.Key.Value == "feature:layout.split-pane");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_applies_only_overrides_dated_before_the_cut()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        // rejected on 2026-07-12: after SPL-104's issue (07-11), before SPL-105's (07-16)
        _ = await world.Store.AppendOverrides([new MapOverride(OverrideAction.Reject, NodeKey.From("feature:layout.split-pane"), "avery", new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero))], TestContext.Current.CancellationToken);

        var before = (await Graph(world, await SampleOf(world, "SPL-104"))).Graph;
        var after = (await Graph(world, await SampleOf(world, "SPL-105"))).Graph;

        Assert.Contains(before.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.DoesNotContain(after.Nodes, n => n.Key.Value == "feature:layout.split-pane");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_imported_map_counts_from_its_import_date()
    {
        await using var late = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await late.ImportMap();   // imported in October: no July cut had it
        await using var early = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await early.ImportMap();

        var without = (await Graph(late, await SampleOf(late, "SPL-105"))).Graph;
        var with = (await Graph(early, await SampleOf(early, "SPL-105"))).Graph;

        Assert.DoesNotContain(without.Nodes, n => n.Key.Value == "feature:layout.split-pane");   // only the extractor's nodes
        Assert.Contains(with.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.Contains(without.Nodes, n => n.Key.Kind == NodeKind.Surface);   // the extractor still maps what the code at the cut shows
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_clusters_join_the_as_of_map_without_labels_and_the_cache_key_records_them()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        var sample = await SampleOf(world, "SPL-105");
        var asked = world.Clusters.Asked.Count;

        var plain = await Graph(world, sample);
        var again = await Graph(world, sample);

        Assert.False(plain.Clustered);
        Assert.True(again.Cached);
        Assert.Equal(plain.Graph.Nodes.Select(n => n.Key.Value), again.Graph.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.True(world.Clusters.Asked.Count > asked);
        Assert.Empty(world.Model.Requests);   // no labeling pass, no model call (§9.1)
    }
}
