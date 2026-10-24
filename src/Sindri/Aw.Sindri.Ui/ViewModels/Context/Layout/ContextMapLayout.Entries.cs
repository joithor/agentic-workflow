using System.Globalization;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

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

    private sealed record Entry(string Key, string Label, string Kind, MapBand Band, Row Row, MapShape Shape, double Width, bool IsAnchor, bool Confirmed, string Provenance, double Confidence, string Detail, string Path, bool HasConflict, string? LinkId, int Order, bool Untrusted = false)
    {
        internal MapNodeLayout ToLayout(MapRect bounds) => new(Key, Label, Kind, Band, Shape, bounds, IsAnchor, Confirmed, Provenance, Confidence, Detail, Path, Row == Row.Tests, HasConflict, LinkId, Untrusted);
    }

    private static List<Entry> Entries(LensesView view)
    {
        var entries = new List<Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in IntentEntries(view).Concat(ProductEntries(view)).Concat(CodeEntries(view.Bands.Code)))
        {
            if (seen.Add(entry.Key))
            {
                entries.Add(entry with { Order = entries.Count });
            }
        }

        return entries;
    }

    private static IEnumerable<Entry> IntentEntries(LensesView view)
    {
        var inConflict = view.Conflicts.SelectMany(c => new[] { c.Decision, c.Other }).Where(k => k is not null).ToHashSet(StringComparer.Ordinal);
        var intent = view.Bands.Intent;
        foreach (var r in intent.Records)
        {
            yield return new Entry(r.Key, r.Label, r.Kind, MapBand.Intent, Row.Intent, MapShape.Chip, Width: 220, IsAnchor: false, Confirmed: true, Provenance: "human", Confidence: 1, Detail: r.Source, Path: string.Empty, HasConflict: false, LinkId: null, Order: 0, Untrusted: IsUntrusted(r.Trust));
        }

        foreach (var d in intent.Decisions.OrderByDescending(d => d.At).ThenBy(d => d.Node, StringComparer.Ordinal))
        {
            var when = d.At.ToString("MMM d", CultureInfo.InvariantCulture);
            yield return new Entry(d.Node, d.Label, d.Kind, MapBand.Intent, Row.Intent, MapShape.Card, Width: 250, IsAnchor: false, d.Confirmed, d.Provenance, Confidence: 1, when, Path: string.Empty, inConflict.Contains(d.Node), LinkId: null, Order: 0, Untrusted: IsUntrusted(d.Trust));
        }
    }

    private static IEnumerable<Entry> ProductEntries(LensesView view)
    {
        var anchors = view.Anchors.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var product = view.Bands.Product;
        var acting = product.Edges.Concat(view.Edges).Where(e => _actingTypes.Contains(e.Type)).Select(e => e.From).ToHashSet(StringComparer.Ordinal);
        foreach (var n in product.Own.Concat(product.Neighbors).Concat(product.Affects))
        {
            var anchor = anchors.Contains(n.Node);
            yield return new Entry(n.Node, n.Label, n.Kind, MapBand.Product, ProductRow(n, anchor, acting), anchor ? MapShape.Card : MapShape.Chip, anchor ? 280 : 170, anchor, n.Confirmed, n.Provenance, n.Confidence, Detail: string.Empty, Path: string.Empty, HasConflict: false, LinkId: null, Order: 0, Untrusted: IsUntrusted(n.Trust));
        }
    }

    private static IEnumerable<Entry> CodeEntries(CodeBand code)
    {
        foreach (var f in code.Files)
        {
            var test = f.Tests.Count > 0;
            var slash = f.Path.LastIndexOf('/');
            var name = slash < 0 ? f.Path : f.Path[(slash + 1)..];
            var dir = slash < 0 ? string.Empty : f.Path[..slash];
            yield return new Entry("code:" + f.Path, name, test ? "test" : "file", MapBand.Code, test ? Row.Tests : Row.Files, MapShape.Chip, test ? 176 : 150, IsAnchor: false, f.Confirmed, f.Provenance, f.Confidence, dir, f.Path, HasConflict: false, f.LinkId, Order: 0, Untrusted: IsUntrusted(f.Trust));
        }
    }

    private static readonly HashSet<string> _actingTypes = new(["on", "reads", "writes"], StringComparer.Ordinal);

    private static bool IsUntrusted(string trust) => string.Equals(trust, "untrusted", StringComparison.Ordinal);

    private static Row ProductRow(ProductNode n, bool anchor, HashSet<string> acting) =>
        anchor || string.Equals(n.Kind, "feature", StringComparison.Ordinal) ? Row.Features
        : string.Equals(n.Kind, "action", StringComparison.Ordinal) || string.Equals(n.Kind, "data", StringComparison.Ordinal) || acting.Contains(n.Node) ? Row.Actions
        : Row.Surfaces;
}
