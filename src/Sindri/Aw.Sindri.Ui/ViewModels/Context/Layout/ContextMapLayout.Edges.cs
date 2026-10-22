using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

internal static partial class ContextMapLayout
{
    // The edges of both lists between nodes that are on the map, once each; a conflict is drawn as one dashed "conflicts" edge in place of the other edges between its two decisions.
    private static List<LensEdge> Links(LensesView view, List<Entry> entries)
    {
        var keys = entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var edges = view.Bands.Product.Edges.Concat(view.Edges)
            .Where(e => keys.Contains(e.From) && keys.Contains(e.To) && !string.Equals(e.From, e.To, StringComparison.Ordinal))
            .DistinctBy(e => e.Link ?? $"{e.From}>{e.To}:{e.Type}", StringComparer.Ordinal)
            .ToList();
        var pairs = new HashSet<(string, string)>();
        var conflicts = new List<LensEdge>();
        foreach (var c in view.Conflicts)
        {
            if (c.Decision is { } a && c.Other is { } b && keys.Contains(a) && keys.Contains(b) && pairs.Add(Pair(a, b)))
            {
                conflicts.Add(new LensEdge(a, b, "conflicts", "deterministic", Link: null, Confirmed: false, Confidence: 1));
            }
        }

        _ = edges.RemoveAll(e => pairs.Contains(Pair(e.From, e.To)));
        edges.AddRange(conflicts);
        return edges;
    }

    private static (string, string) Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    private static List<MapEdgeLayout> Route(List<LensEdge> edges, List<MapNodeLayout> nodes)
    {
        var at = nodes.ToDictionary(n => n.Key, StringComparer.Ordinal);
        var laid = new List<MapEdgeLayout>();
        foreach (var e in edges)
        {
            var (a, b) = (at[e.From], at[e.To]);
            var route = Path(a, b);
            var dashed = !e.Confirmed || string.Equals(e.Provenance, "model", StringComparison.Ordinal);
            var word = Labels.ContextMapEdgeWord(e.Type);
            var suggested = !e.Confirmed && string.Equals(e.Provenance, "model", StringComparison.Ordinal);
            var label = suggested ? word + " · " + Labels.ContextMapSuggestedPct((int)Math.Round(e.Confidence * 100, MidpointRounding.AwayFromZero)) : word;
            laid.Add(new MapEdgeLayout(e.Link ?? $"{e.From}>{e.To}:{e.Type}", e.From, e.To, e.Type, route, dashed, a.Band != b.Band, label, route.Midpoint(), e.Link));
        }

        return laid;
    }

    private static MapPoint Top(MapRect r) => new(r.Center.X, r.Top);

    private static MapPoint Bottom(MapRect r) => new(r.Center.X, r.Bottom);

    private static EdgeRoute Path(MapNodeLayout a, MapNodeLayout b)
    {
        if (a.Band != b.Band)
        {
            var (up, down) = a.Band < b.Band ? (a.Bounds, b.Bounds) : (b.Bounds, a.Bounds);
            return EdgeRoute.Curve(Bottom(up), Top(down));
        }

        var (left, right) = a.Bounds.Center.X <= b.Bounds.Center.X ? (a.Bounds, b.Bounds) : (b.Bounds, a.Bounds);
        var sameRow = Math.Abs(a.Bounds.Center.Y - b.Bounds.Center.Y) < 1;
        if (sameRow && right.Left - left.Right <= 110)
        {
            return EdgeRoute.Straight(new MapPoint(left.Right, left.Center.Y), new MapPoint(right.Left, right.Center.Y));
        }

        if (sameRow)
        {
            return EdgeRoute.Bus(Top(a.Bounds), Top(b.Bounds), Math.Min(a.Bounds.Top, b.Bounds.Top) - 18);
        }

        var (upper, lower) = a.Bounds.Center.Y <= b.Bounds.Center.Y ? (a.Bounds, b.Bounds) : (b.Bounds, a.Bounds);
        if (upper.Bottom + 8 <= lower.Top)
        {
            return EdgeRoute.Bus(Top(lower), Bottom(upper), upper.Bottom + 22);
        }

        // Rows that overlap in height (a ticket beside a decision): out of one side, in at the other.
        var (from, to) = (new MapPoint(left.Right, left.Center.Y), new MapPoint(right.Left, right.Center.Y));
        var middle = (from.X + to.X) / 2;
        return EdgeRoute.Elbow([from, new MapPoint(middle, from.Y), new MapPoint(middle, to.Y), to]);
    }
}
