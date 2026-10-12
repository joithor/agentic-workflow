using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Map;

public sealed class MapDocumentTests
{
    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\n";

    [Fact, Trait("Lane", "unit")]
    public void The_fixture_map_checks_with_its_decision_node_and_quotes_edge_added()
    {
        var map = Maps.Checked(Maps.Fixture());

        Assert.Equal(new MapCounts(Nodes: 19, Edges: 19, Aliases: 1, Decisions: 1, Overrides: 2), map.Counts);
        var decision = map.Graph.Decisions[0].Key;
        Assert.Contains(map.Graph.Edges, e => e.From == decision && e.Type == EdgeType.Quotes
            && e.To.Value == "record:linear:issue/0e5a1b00-0000-4000-8000-000000000103");
        Assert.Equal("feature:layout.split-pane.orientation", map.Graph.Resolve(Maps.Key("feature:layout.orientation"))?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_authored_map_defaults_to_human_provenance_and_keeps_what_the_file_says()
    {
        var map = Maps.Checked(Maps.Fixture());

        Assert.Equal(Provenance.Human, map.Graph.Resolve(Maps.Key("feature:layout.split-pane"))?.Provenance);
        Assert.Equal(Provenance.Model, map.Graph.Resolve(Maps.Key("feature:layout.drag-to-split"))?.Provenance);
        Assert.Equal(DateTimeOffset.Parse("2026-07-15T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), map.Graph.Decisions[0].At);
    }

    [Fact, Trait("Lane", "unit")]
    public void Keys_pass_through_EntryKey_on_import()
    {
        var map = Maps.Checked(Maps.Parse(Header + "nodes:\n  - {key: \"feature:Layout.Split-Pane\", label: Split}\n"));

        Assert.Equal("feature:layout.split-pane", map.Graph.Nodes[0].Key.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("  - {from: \"feature:a\", to: \"thing:t\", type: reads}\n", "SND-CONTEXT-031", "edges[0].type: 'reads' doesn't go from a feature to a thing")]
    [InlineData("  - {from: \"feature:a\", to: \"feature:missing\", type: part-of}\n", "SND-CONTEXT-032", "edges[0].to: 'feature:missing' isn't defined")]
    [InlineData("  - {from: \"feature:a\", to: \"feature:b\", type: depends-on, confidence: 2}\n", "SND-CONTEXT-031", "edges[0].confidence: 2 is outside 0 to 1")]
    public void A_bad_edge_names_its_key_path_and_nothing_is_imported(string edge, string code, string message)
    {
        var yaml = Header + "nodes:\n  - {key: \"feature:a\", label: A}\n  - {key: \"feature:b\", label: B}\n  - {key: \"thing:t\", label: T}\nedges:\n" + edge;

        var check = MapDocumentValidator.Check(Maps.Parse(yaml), KeyMigrations.Shipped);

        Assert.True(check.IsError);
        Assert.Equal(code, check.Error.Code);
        Assert.Contains(message, check.Error.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was written", check.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("schemaVersion: 2\nkind: authored\nrepo: r\nnodes: []\n", "schemaVersion: 2 isn't supported")]
    [InlineData("schemaVersion: 1\nkind: authored\nrepo: r\nkeyVersion: 9\nnodes: []\n", "keyVersion: 9 isn't supported")]
    [InlineData(Header + "nodes:\n  - {key: \"surface:widget/x\", label: X}\n", "nodes[0].key: 'surface:widget/x' needs a surface kind")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: A}\n  - {key: \"feature:A\", label: A2}\n", "nodes[1].key: 'feature:a' is defined twice")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: \" \"}\n", "nodes[0].label: a label is required")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: A}\naliases:\n  - {alias: \"feature:a\", target: \"feature:a\"}\n", "aliases[0].alias: 'feature:a' is a node's own key")]
    public void A_bad_map_is_SND_CONTEXT_031_with_the_key_path(string yaml, string message)
    {
        var check = MapDocumentValidator.Check(Maps.Parse(yaml), KeyMigrations.Shipped);

        Assert.True(check.IsError);
        Assert.Equal(ContextErrors.MapFileInvalid.Code, check.Error.Code);
        Assert.Contains(message, check.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Shape_problems_win_over_undefined_references_and_every_problem_is_listed()
    {
        var yaml = Header + "nodes:\n  - {key: \"surface:widget/x\", label: X}\n  - {key: \"feature:a\", label: A}\n" +
            "edges:\n  - {from: \"feature:a\", to: \"feature:nope\", type: part-of}\n  - {from: \"feature:a\", to: \"feature:a\", type: on}\n";

        var check = MapDocumentValidator.Check(Maps.Parse(yaml), KeyMigrations.Shipped);

        Assert.Equal(ContextErrors.MapFileInvalid.Code, check.Error.Code);
        Assert.Contains("nodes[0].key", check.Error.Message, StringComparison.Ordinal);
        Assert.Contains("edges[1].type", check.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_override_on_a_node_the_file_lacks_is_kept_as_orphaned_not_refused()
    {
        var yaml = Header + "nodes:\n  - {key: \"feature:a\", label: A}\noverrides:\n  - {action: delete, target: \"feature:gone\", who: avery, at: \"2026-07-30T09:00:00Z\"}\n";

        _ = Assert.Single(Maps.Checked(Maps.Parse(yaml)).Overrides);
    }
}
