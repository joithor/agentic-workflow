using System.Globalization;
using System.Text;
using Aw.Context.Core.Lenses;

namespace Aw.Sindri.Host.Text;

/// <summary>Plain text for 'context lenses' and 'context filemap': every status is a word ('suggested', 'unmapped', 'conflict'), never colour.</summary>
internal static class ContextLensText
{
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    internal static string Lenses(LensesView view)
    {
        var text = new StringBuilder(Invariant($"lenses of {view.Anchor.Value} · map {view.Map}{(view.Draft ? " (draft)" : string.Empty)}\n"));
        Band(text, "intent", view.Intent);
        Band(text, "product", view.Product);
        Band(text, "code", view.Code);
        _ = text.AppendLine(Invariant($"symbols: {view.Symbols.Status} ({view.Symbols.Reason})"));
        foreach (var edge in view.Edges)
        {
            _ = text.AppendLine(Invariant($"edge {edge.From.Value} {edge.Type.ToString().ToLowerInvariant()} {edge.To.Value}  {(edge.Confirmed ? "confirmed" : "suggested")} {edge.Confidence:0.00}  {edge.Link.Value}"));
        }

        foreach (var conflict in view.Conflicts)
        {
            _ = text.AppendLine($"conflict {conflict.Kind}: {conflict.Newer.Value} over {conflict.Older.Value}");
        }

        var c = view.Coverage;
        _ = text.AppendLine(Invariant($"coverage: {c.Covered} of {c.Scope} in scope have a record, {c.Confirmed} confirmed, {c.Implemented} implemented in code"));
        foreach (var gap in view.Gaps)
        {
            _ = text.Append("gap: ").AppendLine(gap);
        }

        return text.ToString();
    }

    internal static string FileMap(FileMapView view)
    {
        var text = new StringBuilder(Invariant($"{view.Repo.Value} {view.Path} · map {view.Map}{(view.Draft ? " (draft)" : string.Empty)}\n"));
        if (view.Entries.Count == 0)
        {
            _ = text.AppendLine("  nothing here");
        }

        foreach (var entry in view.Entries)
        {
            var features = entry.Features.Count == 0 ? string.Empty : "  " + string.Join(", ", entry.Features.Select(f => f.Value));
            _ = text.AppendLine(Invariant($"  {entry.Name + (entry.Directory ? "/" : string.Empty),-28} {entry.Status.ToString().ToLowerInvariant(),-10} {entry.Files} {(entry.Files == 1 ? "file" : "files")}{features}"));
        }

        foreach (var s in view.Suggested)
        {
            _ = text.AppendLine(Invariant($"  suggested {s.File.Value} implements {s.Feature.Value} {s.Confidence:0.00}  accept: sindri context filemap accept {s.Link.Value}"));
        }

        var t = view.Totals;
        _ = text.AppendLine(Invariant($"{t.Mapped} mapped, {t.Suggested} suggested, {t.Unmapped} unmapped · symbols: {view.Symbols.Status} ({view.Symbols.Reason})"));
        return text.ToString();
    }

    private static void Band(StringBuilder text, string name, LensBand band)
    {
        _ = text.AppendLine(Invariant($"{name} ({band.Items.Count}{(band.Truncated ? ", truncated" : string.Empty)}):"));
        if (band.Items.Count == 0)
        {
            _ = text.AppendLine("  none");
        }

        foreach (var item in band.Items)
        {
            _ = text.AppendLine(Invariant($"  {item.Label}  {item.Relation.ToString().ToLowerInvariant()}  {item.Provenance.ToString().ToLowerInvariant()} {item.Confidence:0.00}  {(item.Confirmed ? "confirmed" : "suggested")}  {item.Key.Value}{(item.Link is { } link ? "  " + link.Value : string.Empty)}"));
        }
    }
}
