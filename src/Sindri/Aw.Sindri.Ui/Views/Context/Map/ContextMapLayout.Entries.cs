using System.Globalization;
using Avalonia;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Context.Map;

internal static partial class ContextMapLayout
{
    // The row a node goes in; the order is the order the rows run down the world.
    private enum Row
    {
        Intent,
        Features,
        Surfaces,
        Actions,
        Files,
        Tests,
    }

    private sealed record Entry(string Key, string Label, string Kind, MapBand Band, Row Row, MapShape Shape, double Width, bool IsAnchor, bool Confirmed, string Provenance, double Confidence, string Detail, string Path, bool HasConflict, string? LinkId, int Order)
    {
        internal MapNodeLayout ToLayout(Rect bounds) => new(Key, Label, Kind, Band, Shape, bounds, IsAnchor, Confirmed, Provenance, Confidence, Detail, Path, Row == Row.Tests, HasConflict, LinkId);
    }

    private static List<Entry> Entries(LensesView view)
    {
        var entries = new List<Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(Entry entry)
        {
            if (seen.Add(entry.Key))
            {
                entries.Add(entry with { Order = entries.Count });
            }
        }

        var inConflict = view.Conflicts.SelectMany(c => new[] { c.Decision, c.Other }).Where(k => k is not null).ToHashSet(StringComparer.Ordinal);
        var intent = view.Bands.Intent;
        foreach (var r in intent.Records)
        {
            Add(new Entry(r.Key, r.Label, r.Kind, MapBand.Intent, Row.Intent, MapShape.Chip, 220, false, true, "human", 1, r.Source, string.Empty, false, null, 0));
        }

        foreach (var d in intent.Decisions.OrderByDescending(d => d.At).ThenBy(d => d.Node, StringComparer.Ordinal))
        {
            Add(new Entry(d.Node, d.Label, d.Kind, MapBand.Intent, Row.Intent, MapShape.Card, 250, false, d.Confirmed, d.Provenance, 1, d.At.ToString("MMM d", CultureInfo.InvariantCulture), string.Empty, inConflict.Contains(d.Node), null, 0));
        }

        if (intent.Truncated)
        {
            Add(More(MapBand.Intent, Row.Intent));
        }

        var anchors = view.Anchors.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var product = view.Bands.Product;
        var acting = product.Edges.Concat(view.Edges).Where(e => e.Type is "on" or "reads" or "writes").Select(e => e.From).ToHashSet(StringComparer.Ordinal);
        foreach (var n in product.Own.Concat(product.Neighbors).Concat(product.Affects))
        {
            var anchor = anchors.Contains(n.Node);
            Add(new Entry(n.Node, n.Label, n.Kind, MapBand.Product, ProductRow(n, anchor, acting), anchor ? MapShape.Card : MapShape.Chip, anchor ? 280 : 170, anchor, n.Confirmed, n.Provenance, n.Confidence, string.Empty, string.Empty, false, null, 0));
        }

        if (product.Truncated)
        {
            Add(More(MapBand.Product, Row.Features));
        }

        foreach (var f in view.Bands.Code.Files)
        {
            var test = f.Tests.Count > 0;
            var slash = f.Path.LastIndexOf('/');
            var name = slash < 0 ? f.Path : f.Path[(slash + 1)..];
            var dir = slash < 0 ? string.Empty : f.Path[..slash];
            Add(new Entry("code:" + f.Path, name, test ? "test" : "file", MapBand.Code, test ? Row.Tests : Row.Files, MapShape.Chip, test ? 176 : 150, false, f.Confirmed, f.Provenance, f.Confidence, dir, f.Path, false, f.LinkId, 0));
        }

        if (view.Bands.Code.Truncated)
        {
            Add(More(MapBand.Code, Row.Files));
        }

        return entries;
    }

    private static Row ProductRow(ProductNode n, bool anchor, HashSet<string> acting) =>
        anchor || string.Equals(n.Kind, "feature", StringComparison.Ordinal) ? Row.Features
        : n.Kind is "action" or "data" || acting.Contains(n.Node) ? Row.Actions
        : Row.Surfaces;

    // The chip that says the band was cut short.
    private static Entry More(MapBand band, Row row) => new("more:" + band.ToString().ToLowerInvariant(), Labels.ContextTruncated, "more", band, row, MapShape.Chip, 170, false, true, "human", 1, string.Empty, string.Empty, false, null, 0);
}
