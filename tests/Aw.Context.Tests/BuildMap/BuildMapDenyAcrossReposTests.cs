using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Review #234 run 3 (testing-reviewer-0, correctness-reviewer-0): the build judges records and evidence by every profile repo's globs, and code paths only by the repos it maps.</summary>
public sealed class BuildMapDenyAcrossReposTests : IDisposable
{
    private const string PrivateRecord = "record:repo:other/docs/private/x.md";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Denying(string path) =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
        [
            new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes"),
            new ContextRepo(ContextRepoName.From("other"), "/work/other") { DenyPaths = [.. PathDenylist.Defaults, path] },
        ]);

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_drops_a_record_of_another_profile_repo_s_denied_document_and_leaves_no_orphaned_accept_though_context_repos_leaves_that_repo_out()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\"}",
            $"  - {{key: \"{PrivateRecord}\", label: \"Private note\"}}",
            "edges:",
            $"  - {{from: \"{PrivateRecord}\", to: \"feature:in\", type: mentions}}"));
        var target = NodeKey.From(PrivateRecord);
        _ = await _world.Store.AppendOverrides([new MapOverride(OverrideAction.Accept, target, "avery", _world.Clock.GetUtcNow())], MapWorld.None);

        _ = await _world.Map.BuildMap(progress: null, Denying("docs/private/**"), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var queue = (await _world.Map.ReviewQueue(limit: null, Denying("docs/private/**"), MapWorld.None)).Value;
        Assert.DoesNotContain(draft.Graph.Nodes, n => n.Key.Value == PrivateRecord);
        Assert.DoesNotContain(queue.Entries, e => e.Group == ReviewGroup.Orphaned || e.Node.Value == PrivateRecord);   // why: a decision about a key a glob denies outright is dropped on every read, not undone
        Assert.Contains(draft.Graph.Nodes, n => n.Key.Value == "feature:in");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_keeps_the_code_nodes_and_a_person_s_accept_when_only_a_repo_context_repos_leaves_out_denies_that_path()
    {
        _world.Clusters.Set = new ClusterSet([new FileCluster(ClusterId.From("c1"), ["src/commands/ids.ts", "src/commands/registry.ts"], 0.7)], Available: true);
        _ = await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);
        var feature = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph.Nodes.First(n => n.Key.Kind == NodeKind.Feature && n.Provenance == Provenance.Model).Key;
        var rest = await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), MapWorld.Human, MapWorld.None);
        Assert.True(rest.IsSuccess, rest.IsError ? rest.Error.Message : null);
        var queue = (await _world.Map.ReviewQueue(limit: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;
        foreach (var entry in queue.Entries)
        {
            var accepted = await _world.Map.ApplyReview(entry.Link is { } link ? new ReviewDecision(ReviewVerb.Accept, Link: link) : new ReviewDecision(ReviewVerb.Accept, entry.Node), MapWorld.Human, MapWorld.None);
            Assert.True(accepted.IsSuccess, accepted.IsError ? accepted.Error.Message : null);
        }

        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);

        _ = await _world.Map.BuildMap(progress: null, Denying("src/commands/**"), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.Contains(draft.Graph.Nodes, n => n.Key.Value == "code:src/commands/ids.ts");
        Assert.Contains(draft.Graph.Nodes, n => n.Key.Value == feature.Value);
        Assert.DoesNotContain(log, o => o.IsCutUndo);
        Assert.Contains(OverrideLog.Active(log), o => o.Target.Value == feature.Value);
    }
}
