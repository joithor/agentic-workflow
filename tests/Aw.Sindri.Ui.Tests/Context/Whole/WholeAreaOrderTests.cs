using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The left-to-right order of the areas (whole-map spec §3.2): joined areas sit side by side, ties go by key, the unassigned area is last.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeAreaOrderTests
{
    private static WholeArea Area(string key, bool unassigned = false) => new(key, key, "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), unassigned);

    private static int Gap(IReadOnlyList<string> order, string a, string b) => Math.Abs(order.ToList().FindIndex(k => string.Equals(k, a, StringComparison.Ordinal)) - order.ToList().FindIndex(k => string.Equals(k, b, StringComparison.Ordinal)));

    private static WholeAreaEdge Edge(string from, string to, int count = 1) => new(from, to, "depends-on", count, 0);

    [Fact]
    public void Without_edges_the_areas_run_by_key_and_the_unassigned_one_is_last()
    {
        var order = WholeAreaOrder.Of([Area("area:unassigned", unassigned: true), Area("feature:b"), Area("feature:a")], []);

        Assert.Equal(["feature:a", "feature:b", "area:unassigned"], order, StringComparer.Ordinal);
    }

    [Fact]
    public void Areas_joined_by_edges_sit_next_to_each_other()
    {
        var order = WholeAreaOrder.Of([Area("a"), Area("b"), Area("c"), Area("d")], [Edge("a", "d", 5), Edge("b", "c", 5)]);

        Assert.Equal(1, Gap(order, "a", "d"));
        Assert.Equal(1, Gap(order, "b", "c"));
    }

    [Fact]
    public void The_order_is_the_same_whatever_order_the_areas_and_edges_arrive_in()
    {
        var areas = new[] { Area("a"), Area("b"), Area("c"), Area("d") };
        var edges = new[] { Edge("a", "d", 5), Edge("b", "c", 5), Edge("c", "a") };

        var forward = WholeAreaOrder.Of(areas, edges);
        var backward = WholeAreaOrder.Of([.. areas.Reverse()], [.. edges.Reverse()]);

        Assert.Equal(forward, backward, StringComparer.Ordinal);
    }

    [Fact]
    public void An_edge_to_an_unknown_area_or_to_itself_moves_nothing()
    {
        var order = WholeAreaOrder.Of([Area("a"), Area("b")], [Edge("a", "ghost"), Edge("b", "b")]);

        Assert.Equal(["a", "b"], order, StringComparer.Ordinal);
    }
}
