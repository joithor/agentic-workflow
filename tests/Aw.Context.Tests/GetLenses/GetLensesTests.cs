using Aw.Context.Core;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.GetLenses;

public sealed class GetLensesTests : IDisposable
{
    private const string Anchor = "feature:tabs.tab-manager";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task ImportTabs() =>
        _ = await _world.ImportYaml(MapWorld.TabsMap);

    [Fact, Trait("Lane", "unit")]
    public async Task The_tabs_map_shows_three_bands_one_conflict_and_the_suggested_file_link()
    {
        await ImportTabs();

        var view = (await _world.Engine.Lenses(Anchor, draft: false, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal((false, "v1", Anchor), (view.Draft, view.Map, view.Anchor.Value));
        Assert.Equal(3, view.Intent.Items.Count);
        Assert.Equal("supersedes", Assert.Single(view.Conflicts).Kind);
        var suggested = Assert.Single(view.Code.Items, i => !i.Confirmed);
        Assert.Equal(("code:src/tabs/TabGroups.ts", 0.74, Provenance.Model), (suggested.Key.Value, suggested.Confidence, suggested.Provenance));
        Assert.True(suggested.Link.HasValue);
        Assert.Equal(view.Product.Items.Count(i => i.Relation == LensRelation.Own), view.Coverage.Scope);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_scope_the_coverage_counts_agrees_with_the_nodes_of_the_map()
    {
        await ImportTabs();

        var view = (await _world.Engine.Lenses(Anchor, draft: false, MapWorld.Human, MapWorld.None)).Value;
        var status = (await _world.Map.MapStatus(MapWorld.Human, MapWorld.None)).Value;

        Assert.True(view.Coverage.Scope <= status.Counts.Nodes);
        Assert.Equal(1, status.Version.Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_text_a_model_wrote_and_nobody_confirmed_is_fenced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"Drafted B\", provenance: model}",
            "edges:",
            "  - {from: \"feature:b\", to: \"feature:a\", type: part-of}"));

        var view = (await _world.Engine.Lenses("feature:a", draft: false, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal("A", view.Product.Items.Single(i => i.Key.Value == "feature:a").Label);
        var model = view.Product.Items.Single(i => i.Key.Value == "feature:b").Label;
        Assert.NotEqual("Drafted B", model, StringComparer.Ordinal);
        Assert.Contains("Drafted B", model, StringComparison.Ordinal);
        Assert.StartsWith("<untrusted", model, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_name_is_not_fenced_even_when_its_link_is_a_models_suggestion()
    {
        await ImportTabs();

        var view = (await _world.Engine.Lenses(Anchor, draft: false, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal("TabGroups.ts", view.Code.Items.Single(i => i.Key.Value == "code:src/tabs/TabGroups.ts").Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Asking_for_the_draft_with_none_is_SND_CONTEXT_044_and_without_a_map_030()
    {
        var none = await _world.Engine.Lenses(Anchor, draft: false, MapWorld.Human, MapWorld.None);
        await ImportTabs();

        var noDraft = await _world.Engine.Lenses(Anchor, draft: true, MapWorld.Human, MapWorld.None);

        Assert.Equal((ContextErrors.NoMap.Code, ContextErrors.NoDraftMap.Code), (none.Error.Code, noDraft.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_anchor_is_SND_CONTEXT_003_with_suggestions()
    {
        await ImportTabs();

        var view = await _world.Engine.Lenses("feature:tabs.no-such-node", draft: false, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, view.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_draft_is_read_when_asked_and_its_map_says_so()
    {
        var draft = (await Drafts.Build(_world)).Draft;

        var view = (await _world.Engine.Lenses("surface:command/split-vertical", draft: true, Drafts.Scope, MapWorld.None)).Value;

        Assert.Equal((true, Drafts.Label(draft)), (view.Draft, view.Map));
        Assert.Contains(view.Code.Items, i => i.Key.Value == "code:src/commands/ids.ts" && i.Confirmed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_inside_its_repos_scope_only_and_not_with_sessions_off()
    {
        _ = await _world.ImportFixture();

        var inside = await _world.Engine.Lenses("feature:layout.split-pane", draft: false, MapWorld.Session(FixtureMap.Repo), MapWorld.None);
        var outside = await _world.Engine.Lenses("feature:layout.split-pane", draft: false, MapWorld.Session("other"), MapWorld.None);
        var off = await _world.Engine.Lenses("feature:layout.split-pane", draft: false, MapWorld.Session(FixtureMap.Repo, enabled: false), MapWorld.None);

        Assert.True(inside.IsSuccess);
        Assert.Equal((ContextErrors.UnknownAnchor.Code, ContextErrors.SessionsDisabled.Code), (outside.Error.Code, off.Error.Code));
    }
}
