using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A band's label counts what the map draws, a node is never dropped for want of a column, and what stays a tile is named in the label.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapBandLabelTests
{
    private static WholeArea Area(string key, params (string Kind, int Count)[] kinds) =>
        new(key, key, "trusted", kinds.Sum(k => k.Count), kinds.ToDictionary(k => k.Kind, k => k.Count, StringComparer.Ordinal), Unassigned: false);

    private static WholeNode Node(string key, string kind, string area) => new(key, key, kind, area, [], "trusted", "human", 1, Confirmed: true);

    [Fact]
    public void A_band_label_counts_what_it_draws()
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var overview = map with { Detail = "areas", Areas = [Area("a", ("feature", 2), ("decision", 1)), Area("b", ("record", 162), ("decision", 6))], AreaEdges = [], Nodes = null, Edges = null };
        var opened = map with
        {
            Area = new WholeAreaInfo("a", Truncated: false, map.Counts),
            Areas = overview.Areas,
            AreaEdges = [],
            Nodes = [Node("n:1", "feature", "a"), Node("n:2", "feature", "a"), Node("n:3", "decision", "a")],
            Edges = [],
        };

        var geometry = WholeMapLayout.PlaceMixed(overview, [opened]);

        var why = geometry.Bands.Single(b => b.Band == WholeBand.Intent).Meta;
        Assert.StartsWith(Labels.ContextWholeKindCount("decision", 1), why, StringComparison.Ordinal);
        Assert.Contains(Labels.ContextWholeBandMore(168, "b"), why, StringComparison.Ordinal);
        Assert.DoesNotContain("162", why, StringComparison.Ordinal);
        Assert.Equal(Labels.ContextWholeKindCount("feature", 2), geometry.Bands.Single(b => b.Band == WholeBand.Product).Meta);
    }

    [Fact]
    public void A_node_with_an_unknown_area_lands_in_not_linked_and_is_counted()
    {
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var placed = map with { Areas = [Area("a", ("feature", 1))], AreaEdges = [], Nodes = [Node("n:1", "feature", "a"), Node("n:2", "record", "area:ghost")], Edges = [] };

        var geometry = WholeMapLayout.Place(placed);

        Assert.Equal(2, geometry.Nodes.Count);
        Assert.Contains(geometry.Columns, c => string.Equals(c.Label, Labels.ContextFilesNoFeature, StringComparison.Ordinal));
        Assert.Equal(Labels.ContextWholeKindCount("record", 1), geometry.Bands.Single(b => b.Band == WholeBand.Intent).Meta);
    }
}
