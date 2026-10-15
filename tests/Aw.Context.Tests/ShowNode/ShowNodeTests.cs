using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowNode;

public sealed class ShowNodeTests : IDisposable
{
    private const string Orientation = "feature:layout.split-pane.orientation";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task ShowNode_by_alias_gives_the_node_its_aliases_and_its_edges_both_ways()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.ShowNode("feature:layout.orientation", MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(("v1", Orientation, "Split orientation"), (view.MapVersion, view.Node.Key.Value, view.Node.Label));
        Assert.Equal((Provenance.Human, true, Trust.Trusted), (view.Node.Provenance, view.Node.Confirmed, view.Node.Trust));
        Assert.Equal(["feature:layout.orientation"], view.Aliases.Select(a => a.Value), StringComparer.Ordinal);
        Assert.Equal([("part-of", "feature:layout.split-pane")], view.Out.Select(e => (EdgeRules.Name(e.Type), e.To.Value)));
        Assert.Equal(["code:src/layout/SplitPane.ts", "surface:command/toggle-orientation"], view.In.Select(e => e.From.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Overrides_apply_a_renamed_node_shows_its_new_label_and_a_rejected_one_is_unknown()
    {
        _ = await _world.ImportFixture();

        var renamed = await _world.Engine.ShowNode("feature:tabs.tab-manager", MapWorld.Human, MapWorld.None);
        var rejected = await _world.Engine.ShowNode("feature:layout.drag-to-split", MapWorld.Human, MapWorld.None);

        Assert.Equal("Tabs", renamed.Value.Node.Label);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, rejected.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_label_is_scrubbed_and_an_unaccepted_model_label_is_fenced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"A " + FakeScrubber.Secret + " <!-- hidden -->\"}",
            "  - {key: \"feature:b\", label: \"Model said so\", provenance: model, confidence: 0.5}"));

        var a = (await _world.Engine.ShowNode("feature:a", MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None)).Value.Node;
        var b = (await _world.Engine.ShowNode("feature:b", MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None)).Value.Node;

        Assert.Equal("A [REDACTED:fake]", a.Label);
        Assert.Equal(("<untrusted source=\"map\">Model said so</untrusted>", false, Trust.Untrusted), (b.Label, b.Confirmed, b.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_in_the_app_or_the_cli_reads_the_model_label_clean_beside_its_trust_and_confidence()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:b\", label: \"Model said so\", provenance: model, confidence: 0.5}"));

        var app = (await _world.Engine.ShowNode("feature:b", MapWorld.HumanVia(ContextClient.Http), MapWorld.None)).Value.Node;
        var cli = (await _world.Engine.ShowNode("feature:b", MapWorld.Human, MapWorld.None)).Value.Node;

        Assert.Equal(("Model said so", Trust.Untrusted, 0.5), (app.Label, app.Trust, app.Confidence));
        Assert.Equal(("Model said so", Trust.Untrusted, 0.5), (cli.Label, cli.Trust, cli.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_it_is_SND_CONTEXT_030()
    {
        var view = await _world.Engine.ShowNode(Orientation, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, view.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("aw-split-panes", true, Orientation, null)]
    [InlineData("aw-split-panes", true, "thing:pane-layout", "SND-CONTEXT-003")]
    [InlineData("other", true, Orientation, "SND-CONTEXT-003")]
    [InlineData("aw-split-panes", false, Orientation, "SND-CONTEXT-020")]
    public async Task A_session_reaches_only_the_nodes_its_repo_implements(string repo, bool enabled, string key, string? code)
    {
        _ = await _world.ImportFixture();

        var view = await _world.Engine.ShowNode(key, MapWorld.Session(repo, enabled), MapWorld.None);

        Assert.Equal(code, view.IsError ? view.Error.Code : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_label_two_nodes_share_is_SND_CONTEXT_037()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"Same\"}",
            "  - {key: \"feature:b\", label: \"Same\"}"));

        var view = await _world.Engine.ShowNode("Same", MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, view.Error.Code);
    }
}
