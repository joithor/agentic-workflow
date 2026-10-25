using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Subgraph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetSubgraph;

/// <summary>The subgraph read through the engine (scoping spec C14): as of a retained version, scoped for a session, deny globs honoured.</summary>
public sealed class GetSubgraphTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Groups = "feature:tabs.tab-groups";
    private const string OnlyInTwo = "feature:only-in-two";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static MapRead V(string text) => MapRead.Of(draft: false, text).Value;

    private ValueTask<Kernel.Result<SubgraphView>> Ask(string[] keys, int? depth = null, MapRead? read = null, ContextScope? scope = null) =>
        _world.Engine.Subgraph(new SubgraphQuery(keys, depth, read ?? MapRead.Current), scope ?? MapWorld.Human, MapWorld.None);

    private async Task TabsThenOther()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);   // v1
        _world.Clock.Advance(TimeSpan.FromDays(3));
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", $"  - {{key: \"{OnlyInTwo}\", label: \"Two\"}}"));   // v2
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Subgraph_at_v1_holds_the_node_removed_in_v2()
    {
        await TabsThenOther();

        var old = (await Ask([Manager], read: V("v1"))).Value;

        Assert.Equal("v1", old.MapVersion);
        Assert.Equal([Manager], old.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Empty(old.Missing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Subgraph_current_lists_the_removed_node_in_missing()
    {
        await TabsThenOther();

        var now = (await Ask([Manager, OnlyInTwo])).Value;

        Assert.Equal([OnlyInTwo], now.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal([Manager], now.Missing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_subgraph_out_of_scope_key_is_missing_exactly_like_absent()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var own = (await Ask([Manager, "feature:nope.x"], scope: MapWorld.Session("aw-split-panes"))).Value;
        var other = (await Ask([Manager, "feature:nope.x"], scope: MapWorld.Session("other"))).Value;

        Assert.Equal(["feature:nope.x"], own.Missing);
        Assert.Equal([Manager, "feature:nope.x"], other.Missing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_subgraph_depth_2_returns_nothing_from_another_repo()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var other = (await Ask([Manager, Groups], depth: 2, scope: MapWorld.Session("other"))).Value;

        Assert.Empty(other.Nodes);
        Assert.Empty(other.Bands.Product.Own);
        Assert.Empty(other.Bands.Product.Neighbors);
        Assert.Empty(other.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Denied_key_is_missing_and_its_edges_dropped()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var denying = new ContextScope(Settings: null, [new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        var view = (await Ask([Manager, "code:src/commands/ids.ts"], depth: 2, scope: denying)).Value;

        Assert.Equal(["code:src/commands/ids.ts"], view.Missing);
        Assert.DoesNotContain(view.Edges, e => e.From.Value.Contains("src/commands/ids.ts", StringComparison.Ordinal) || e.To.Value.Contains("src/commands/ids.ts", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Bands.Code.Files, f => f.Path.Contains("src/commands/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task All_keys_missing_is_a_valid_answer_not_003()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var view = await Ask(["feature:nope.x", "feature:nope.y"]);

        Assert.True(view.IsSuccess);
        Assert.Equal(["feature:nope.x", "feature:nope.y"], view.Value.Missing);
        Assert.Empty(view.Value.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Subgraph_unretained_version_is_110()
    {
        await TabsThenOther();

        var view = await Ask([Manager], read: V("v99"));

        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, view.Error.Code);
    }
}
