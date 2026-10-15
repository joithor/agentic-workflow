using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>A deny glob added to the profile after the map exists keeps its code nodes out of every read before any rebuild (review #220 run 1: security-sentinel-1).</summary>
public sealed class DeniedPathReadTests : IDisposable
{
    private const string Denied = "src/layout/SplitPane.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Denying(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_file_leaves_the_lenses_before_any_rebuild()
    {
        _ = await _world.ImportFixture();

        var before = (await _world.Engine.Lenses("feature:layout.split-pane", draft: false, depth: null, MapWorld.Human, MapWorld.None)).Value;
        var after = (await _world.Engine.Lenses("feature:layout.split-pane", draft: false, depth: null, Denying(Denied), MapWorld.None)).Value;

        Assert.Contains(before.Bands.Code.Files, f => string.Equals(f.Path, Denied, StringComparison.Ordinal));
        Assert.DoesNotContain(after.Bands.Code.Files, f => string.Equals(f.Path, Denied, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_query_for_a_denied_file_is_003_like_any_unknown_anchor()
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: Denied), Denying(Denied), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_node_is_not_shown_and_a_feature_only_it_implemented_goes_with_it()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:drafted\", label: \"Drafted\", provenance: model}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:private/a.ts\", to: \"feature:drafted\", type: implements}",
            "  - {from: \"code:private/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}"));

        var shown = await _world.Engine.ShowNode("code:private/a.ts", Denying("private/**"), MapWorld.None);
        var drafted = await _world.Engine.ShowNode("feature:drafted", Denying("private/**"), MapWorld.None);
        var kept = await _world.Engine.ShowNode("feature:kept", Denying("private/**"), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, shown.Error.Code);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, drafted.Error.Code);
        Assert.True(kept.IsSuccess);
    }
}
