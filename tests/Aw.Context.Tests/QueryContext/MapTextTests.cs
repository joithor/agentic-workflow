using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>Map text at rest and on the way out (spec §7 "Stored text"): human map text is stored as written and cleaned on every answer.</summary>
public sealed class MapTextTests : IDisposable
{
    private const string Quote = "ship it " + FakeScrubber.Secret + " <!-- planted -->";
    private const string Wording = "Ship it " + FakeScrubber.Secret + " <!-- planted -->";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string Decision => DecisionRow.KeyFor(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000301"), Quote).Value;

    private Task<Context.Core.Map.MapImported> Import() => _world.ImportYaml(MapWorld.Yaml(
        "schemaVersion: 1",
        "kind: authored",
        "repo: aw-split-panes",
        "nodes:",
        "  - {key: \"feature:a\", label: \"A " + FakeScrubber.Secret + "\"}",
        "  - {key: \"feature:b\", label: \"B\"}",
        "edges:",
        "  - {from: \"feature:a\", to: \"feature:b\", type: depends-on}",
        "  - {from: \"" + Decision + "\", to: \"feature:a\", type: decides}",
        "decisions:",
        "  - {record: \"linear:issue/0e5a1b00-0000-4000-8000-000000000301\", quote: \"" + Quote + "\", wording: \"" + Wording + "\", at: 2026-07-15T10:00:00Z}"));

    [Fact, Trait("Lane", "unit")]
    public async Task Human_map_text_is_stored_raw_and_redacted_in_show_neighbors_path_and_query()
    {
        _ = await Import();

        var stored = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!;
        var shown = (await _world.Engine.ShowNode("feature:a", MapWorld.Human, MapWorld.None)).Value;
        var neighbors = (await _world.Engine.Neighbors("feature:b", depth: 1, MapWorld.Human, MapWorld.None)).Value;
        var path = (await _world.Engine.Path("feature:b", "feature:a", MapWorld.Human, MapWorld.None)).Value;
        var query = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:a"), MapWorld.Human, MapWorld.None)).Value;

        Assert.Contains(stored.Graph.Nodes, n => n.Label.Contains(FakeScrubber.Secret, StringComparison.Ordinal));
        Assert.Contains(stored.Graph.Decisions, d => d.Quote.Contains(FakeScrubber.Secret, StringComparison.Ordinal) && d.Wording.Contains(FakeScrubber.Secret, StringComparison.Ordinal));
        Assert.Equal("A [REDACTED:fake]", shown.Node.Label);
        Assert.Contains(neighbors.Nodes, n => n.Node.Label == "A [REDACTED:fake]");
        Assert.Contains(path.Steps, s => s.Node.Label == "A [REDACTED:fake]");
        Assert.DoesNotContain(FakeScrubber.Secret, Everything(query), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_that_decides_a_reached_node_is_fenced_and_redacted_in_the_decisions_section()
    {
        _ = await Import();

        var query = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:a"), MapWorld.Human, MapWorld.None)).Value;

        var decision = Assert.Single(query.Sections.Single(s => s.Name == "decisions").Items);
        Assert.Equal(["decides→feature:a"], decision.Why);
        Assert.Equal(["C1"], decision.Cites);
        Assert.Equal("Ship it [REDACTED:fake]", decision.Title);
        Assert.StartsWith("<untrusted source=\"map\">", decision.Text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:fake]", decision.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<!--", decision.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_affected_node_relabelled_for_a_weaker_path_is_redacted_and_fenced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"B " + FakeScrubber.Secret + "\"}",
            "edges:",
            "  - {from: \"feature:b\", to: \"feature:a\", type: depends-on, provenance: model}"));

        var query = (await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:a"), MapWorld.Human, MapWorld.None)).Value;

        var affected = Assert.Single(query.Sections.Single(s => s.Name == "affects").Items);
        Assert.DoesNotContain(FakeScrubber.Secret, Everything(query), StringComparison.Ordinal);
        Assert.Contains("[REDACTED:fake]", affected.Title, StringComparison.Ordinal);
        Assert.StartsWith("<untrusted source=\"map\">", affected.Title, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_terms_a_node_query_sends_to_the_sources_come_from_the_cleaned_label()
    {
        _ = await Import();

        _ = await _world.Engine.Query(new ContextQuery(Text: null, Node: "feature:a"), MapWorld.Human, MapWorld.None);

        var terms = _world.Docs.Searches.SelectMany(q => q.Terms).ToList();
        Assert.Equal(["a"], terms.Distinct(StringComparer.Ordinal).ToList());
    }

    private static string Everything(Context.Core.Results.ContextResult result) =>
        string.Join('\n', result.Sections.SelectMany(s => s.Items).SelectMany(i => new[] { i.Title, i.Text }.Concat(i.Path ?? [])));
}
