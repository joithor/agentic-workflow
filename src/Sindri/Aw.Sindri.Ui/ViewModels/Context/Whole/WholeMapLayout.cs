using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// Places the whole map (whole-map spec §3.2). Pure, like ContextMapLayout: the same answer gives the same numbers. A node-level
/// answer is areas as columns (ordered by <see cref="WholeAreaOrder"/>) with the three bands across them, the same bands in every column;
/// an areas-level answer is one tile per area on a grid, in the same order.
/// </summary>
internal static class WholeMapLayout
{
    /// <summary>A node's width.</summary>
    internal const double NodeWidth = 120;

    /// <summary>A node's height.</summary>
    internal const double NodeHeight = 28;

    /// <summary>The space between nodes.</summary>
    internal const double NodeGap = 6;

    /// <summary>How many nodes sit side by side in a band of one column.</summary>
    internal const int Across = 2;

    /// <summary>The width of the space around a column.</summary>
    internal const double ColumnGutter = 24;

    /// <summary>The height of an area's header.</summary>
    internal const double HeaderHeight = 32;

    /// <summary>The height of the space between bands.</summary>
    internal const double BandGap = 40;

    /// <summary>A tile's width.</summary>
    internal const double TileWidth = 220;

    /// <summary>A tile's height.</summary>
    internal const double TileHeight = 110;

    /// <summary>The space between tiles.</summary>
    internal const double TileGap = 32;

    /// <summary>The margin around the world.</summary>
    internal const double Margin = 24;

    private const double ColumnWidth = (Across * (NodeWidth + NodeGap)) + ColumnGutter;
    private const double NodeCell = NodeHeight + NodeGap;

    /// <summary>The band a node kind sits in.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>Decisions and records above, code below, everything else in the product band (an unknown kind too).</returns>
    internal static WholeBand BandOf(string kind) => kind switch
    {
        "decision" or "record" => WholeBand.Intent,
        "code" => WholeBand.Code,
        "feature" or "surface" or "action" or "thing" or "flow" => WholeBand.Product,
        _ => WholeBand.Product,
    };

    /// <summary>Places an answer.</summary>
    /// <param name="map">The answer.</param>
    /// <returns>Where everything sits.</returns>
    internal static WholeMapGeometry Place(WholeMapView map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Area is { } opened)
        {
            // One feature opened in full: its nodes alone, in one column (the others stay tiles in the overview it came from).
            map = map with { Areas = [.. map.Areas.Where(a => string.Equals(a.Key, opened.Key, StringComparison.Ordinal))], AreaEdges = [] };
        }

        var order = WholeAreaOrder.Of(map.Areas, map.AreaEdges);
        return map.Nodes is { Count: > 0 } nodes && !string.Equals(map.Detail, "areas", StringComparison.Ordinal)
            ? Columns(map, order, nodes)
            : Tiles(map, order);
    }

    /// <summary>
    /// Places an overview with some features opened in full (a query zoomed into an answer by feature): the opened ones as columns of nodes, the rest still as
    /// tiles in a block to their right, in the same order.
    /// </summary>
    /// <param name="overview">The answer by area.</param>
    /// <param name="opened">The answers for the opened features (each read with area=).</param>
    /// <returns>Where everything sits.</returns>
    internal static WholeMapGeometry PlaceMixed(WholeMapView overview, IReadOnlyList<WholeMapView> opened)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(opened);
        var openKeys = opened.Where(o => o.Area is not null).Select(o => o.Area!.Key).ToHashSet(StringComparer.Ordinal);   // why: filtered on Area
        var inOrder = WholeAreaOrder.Of(overview.Areas, overview.AreaEdges);
        var open = inOrder.Where(openKeys.Contains).ToList();
        var rest = overview.Areas.Where(a => !openKeys.Contains(a.Key)).ToList();
        var restKeys = rest.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var nodes = opened.SelectMany(o => o.Nodes ?? []).GroupBy(n => n.Key, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var edges = opened.SelectMany(o => o.Edges ?? []).GroupBy(e => e.Link, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var columns = Columns(overview with { Areas = [.. overview.Areas.Where(a => openKeys.Contains(a.Key))], AreaEdges = [], Nodes = nodes, Edges = edges, Detail = "nodes", Area = null }, open, nodes);
        var restEdges = overview.AreaEdges.Where(e => restKeys.Contains(e.From) && restKeys.Contains(e.To)).ToList();
        var tiles = Tiles(overview with { Areas = rest, AreaEdges = restEdges }, WholeAreaOrder.Of(rest, restEdges));
        var shift = columns.World.Right + TileGap;
        var placedTiles = tiles.Tiles.Select(t => t with { AreaIndex = t.AreaIndex + open.Count, Rect = t.Rect with { X = t.Rect.X + shift } }).ToList();
        var areaEdges = tiles.AreaEdges.Select(e => e with { X1 = e.X1 + shift, X2 = e.X2 + shift }).ToList();
        var world = new WholeRect(0, 0, shift + tiles.World.Width, Math.Max(columns.World.Height, tiles.World.Height));
        return new WholeMapGeometry(world, ByArea: false, [.. columns.Order, .. tiles.Order], columns.Columns, columns.Nodes, placedTiles, columns.Edges, areaEdges, columns.Bands);
    }

    private static WholeMapGeometry Tiles(WholeMapView map, IReadOnlyList<string> order)
    {
        var byKey = map.Areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        var across = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(order.Count)));
        var tiles = order.Select((key, i) =>
        {
            var area = byKey[key];
            var rect = new WholeRect(Margin + (i % across * (TileWidth + TileGap)), Margin + (i / across * (TileHeight + TileGap)), TileWidth, TileHeight);
            var kinds = area.ByKind.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).ToList();
            return new PlacedTile(key, area.Label, i, rect, area.Members, kinds, area.Unassigned);
        }).ToList();
        var centre = tiles.ToDictionary(t => t.Key, t => t.Rect, StringComparer.Ordinal);
        var edges = map.AreaEdges.Where(e => centre.ContainsKey(e.From) && centre.ContainsKey(e.To)).Select(e =>
            new PlacedAreaEdge(e.From, e.To, e.Type, e.Count, e.Confirmed, centre[e.From].CenterX, centre[e.From].CenterY, centre[e.To].CenterX, centre[e.To].CenterY)).ToList();
        var rows = order.Count == 0 ? 0 : ((order.Count - 1) / across) + 1;
        var world = new WholeRect(0, 0, (2 * Margin) + (Math.Min(across, Math.Max(1, order.Count)) * (TileWidth + TileGap)) - TileGap, (2 * Margin) + (rows * (TileHeight + TileGap)) - TileGap);
        return new WholeMapGeometry(world, ByArea: true, order, [], [], tiles, [], edges, []);
    }

    private static WholeMapGeometry Columns(WholeMapView map, IReadOnlyList<string> order, IReadOnlyList<WholeNode> nodes)
    {
        var index = order.Select((k, i) => (k, i)).ToDictionary(p => p.k, p => p.i, StringComparer.Ordinal);
        var labels = map.Areas.ToDictionary(a => a.Key, a => a.Label, StringComparer.Ordinal);
        var kept = nodes.Where(n => index.ContainsKey(n.Area)).ToList();
        var groups = kept.GroupBy(n => (Area: index[n.Area], Band: BandOf(n.Kind))).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Kind, StringComparer.Ordinal).ThenBy(n => n.Key, StringComparer.Ordinal).ToList());
        var bands = BandExtents(groups);
        var placed = new List<PlacedNode>(kept.Count);
        foreach (var ((area, band), members) in groups.OrderBy(g => g.Key.Area).ThenBy(g => g.Key.Band))
        {
            var top = bands.First(b => b.Band == band).Top;
            for (var i = 0; i < members.Count; i++)
            {
                var rect = new WholeRect(
                    Margin + (area * ColumnWidth) + (i % Across * (NodeWidth + NodeGap)),
                    top + (i / Across * NodeCell),
                    NodeWidth,
                    NodeHeight);
                placed.Add(new PlacedNode(members[i].Key, members[i].Label, members[i].Kind, area, rect, members[i].Confirmed));
            }
        }

        var height = bands.Count == 0 ? HeaderHeight + (2 * Margin) : bands[^1].Top + bands[^1].Height + Margin;
        var columns = order.Select((key, i) => new AreaColumn(key, labels.GetValueOrDefault(key, key), i, new WholeRect(Margin + (i * ColumnWidth), Margin, ColumnWidth - ColumnGutter, height - (2 * Margin)))).ToList();
        var position = placed.ToDictionary(p => p.Key, StringComparer.Ordinal);
        var edges = (map.Edges ?? []).Where(e => position.ContainsKey(e.From) && position.ContainsKey(e.To)).Select(e =>
            new PlacedEdge(e.From, e.To, e.Type, e.Confirmed, position[e.From].Rect.CenterX, position[e.From].Rect.CenterY, position[e.To].Rect.CenterX, position[e.To].Rect.CenterY, e.Confidence)).ToList();
        var world = new WholeRect(0, 0, (2 * Margin) + (Math.Max(1, order.Count) * ColumnWidth) - ColumnGutter, height);
        return new WholeMapGeometry(world, ByArea: false, order, columns, placed, [], edges, [], bands);
    }

    // The three bands stacked under the headers, each as tall as its fullest column needs, so a band lines up across every column.
    private static List<PlacedBand> BandExtents(Dictionary<(int Area, WholeBand Band), List<WholeNode>> groups)
    {
        var bands = new List<PlacedBand>();
        var top = Margin + HeaderHeight;
        foreach (var band in Enum.GetValues<WholeBand>())
        {
            var rows = groups.Where(g => g.Key.Band == band).Select(g => (int)Math.Ceiling(g.Value.Count / (double)Across)).DefaultIfEmpty(0).Max();
            if (rows == 0)
            {
                continue;
            }

            var height = rows * NodeCell;
            bands.Add(new PlacedBand(band, top, height));
            top += height + BandGap;
        }

        if (bands.Count > 0)
        {
            top -= BandGap;
        }

        return bands;
    }
}
