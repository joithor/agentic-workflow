using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>Review #234 run 3 (correctness-reviewer-0): a 'code:' key names no repo, so another profile repo's deny glob never hides the code of the repo the map was built from.</summary>
public sealed class CodeDenyAcrossReposTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope(ContextCallerKind kind)
    {
        using var section = System.Text.Json.JsonDocument.Parse("""{"sessions":{"enabled":true},"repos":["aw-split-panes"]}""");
        IReadOnlyList<ContextRepo> repos =
        [
            new(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes"),
            new(ContextRepoName.From("other"), "/work/other") { DenyPaths = [.. PathDenylist.Defaults, "src/in/**"] },
        ];
        var caller = kind == ContextCallerKind.Session
            ? new ContextCaller(kind, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http)
            : new ContextCaller(kind, Repo: null, ContextClient.Cli);
        return new(section.RootElement.Clone(), repos, caller);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ContextCallerKind.Session)]
    [InlineData(ContextCallerKind.Human)]
    public async Task Another_profile_repo_denying_a_path_leaves_the_mapped_repo_s_code_node_and_the_feature_it_implements_visible(ContextCallerKind kind)
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\", provenance: model}",
            "  - {key: \"code:src/in/x.ts\", label: \"x.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"code:src/in/x.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in/x.ts\"}]}"));
        var scope = Scope(kind);

        var code = await _world.Engine.ShowNode("code:src/in/x.ts", scope, MapWorld.None);
        var feature = await _world.Engine.ShowNode("feature:in", scope, MapWorld.None);
        var around = await _world.Engine.Neighbors("feature:in", depth: 2, scope, MapWorld.None);

        Assert.True(code.IsSuccess, code.IsError ? code.Error.Message : null);
        Assert.True(feature.IsSuccess, feature.IsError ? feature.Error.Message : null);
        Assert.Contains(around.Value.Nodes, n => n.Node.Key.Value == "code:src/in/x.ts");
    }
}
