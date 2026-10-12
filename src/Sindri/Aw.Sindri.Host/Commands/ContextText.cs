using System.Globalization;
using System.Text;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The text form of context results (context spec §8.4 "Text output"): a header with the anchors and map version, a
/// DEGRADED line per missing source, conflicts before the sections, sections as headings with [Cn] markers and
/// '(draft)' on unconfirmed map items, then the sources table. Every state is a word; there is never colour, so
/// NO_COLOR holds. Untrusted text keeps its fence.
/// </summary>
internal static class ContextText
{
    internal static string Render(ContextResult result)
    {
        var text = new StringBuilder();
        var anchors = result.Anchors.Count == 0 ? "none" : string.Join(", ", result.Anchors);
        _ = text.AppendLine(Invariant($"context {result.QueryId.Value} · anchors: {anchors} · map: {result.MapVersion ?? "none"} · as of {result.AsOf:O}"));
        foreach (var degraded in result.Degraded)
        {
            _ = text.AppendLine(Invariant($"DEGRADED {degraded.Source.Value}: {degraded.Code} {degraded.Message}"));
        }

        foreach (var conflict in result.Conflicts)
        {
            _ = text.AppendLine(Invariant($"CONFLICT {conflict.Node}: {conflict.Summary} (intent {Markers(conflict.Intent)}; behavior {Markers(conflict.Behavior)})"));
        }

        if (result.Sections.Count == 0)
        {
            _ = text.AppendLine("no results");
        }

        foreach (var section in result.Sections)
        {
            Section(text, section);
        }

        _ = text.AppendLine(Invariant($"budget: {result.Budget.Used} of {result.Budget.Asked} tokens{(result.Budget.Estimate ? " (estimate)" : string.Empty)}{(result.Truncated ? ", truncated" : string.Empty)}"));
        Citations(text, result.Cites);
        return text.ToString();
    }

    internal static string Render(SourcesView view)
    {
        var text = new StringBuilder(Invariant($"sessions: {(view.SessionsEnabled ? "on" : "off")} (context.sessions.enabled)\n"));
        foreach (var row in view.Sources)
        {
            var sync = row.LastSync is { } at ? Invariant($", last sync {at:O}") : string.Empty;
            _ = text.AppendLine(Invariant($"{row.Source.Value,-10} {row.Repo ?? "-",-16} {Word(row.State),-11} {row.Detail}{sync}"));
        }

        return view.Sources.Count == 0 ? text.AppendLine("no sources: the profile maps no repo").ToString() : text.ToString();
    }

    private static void Section(StringBuilder text, ResultSection section)
    {
        _ = text.AppendLine().Append("## ").AppendLine(section.Name);
        foreach (var item in section.Items)
        {
            var draft = item.Node is not null && !item.Confirmed ? " (draft)" : string.Empty;
            _ = text.AppendLine(Invariant($"{Markers(item.Cites)} {item.Title ?? item.Node ?? item.Kind}{draft}"));
            _ = text.AppendLine(Invariant($"    trust {(item.Trust == Trust.Trusted ? "trusted" : "untrusted")} · {string.Join(", ", item.Why)}"));
            foreach (var line in (item.Text ?? "(text withheld)").Split('\n'))
            {
                _ = text.Append("    ").AppendLine(line);
            }
        }

        if (section.More.Count > 0)
        {
            _ = text.AppendLine(Invariant($"  +{section.More.Count} more (references only): {string.Join(", ", section.More)}"));
        }

        _ = text.AppendLine();
    }

    private static void Citations(StringBuilder text, IReadOnlyDictionary<string, Citation> cites)
    {
        if (cites.Count == 0)
        {
            return;
        }

        _ = text.AppendLine().AppendLine("sources:");
        foreach (var (marker, cite) in cites.OrderBy(c => int.Parse(c.Key.AsSpan(1), CultureInfo.InvariantCulture)))
        {
            foreach (var reference in cite.Refs)
            {
                var current = reference.Current is { } now ? " → " + Short(now) : string.Empty;
                _ = text.AppendLine(Invariant($"  [{marker}] {cite.Key.Value}  {reference.Source.Value} {reference.Id.Value}  version {Short(reference.Version)}{current}  {Word(cite.State)}"));
            }
        }
    }

    private static string Markers(IReadOnlyList<string> markers) => string.Join(' ', markers.Select(m => "[" + m + "]"));

    private static string Short(SourceVersion version) => version.Value.Length > 12 ? version.Value[..12] : version.Value;

    private static string Word(CitationState state) => state switch
    {
        CitationState.Current => "current",
        CitationState.Stale => "stale",
        CitationState.Unchecked => "unchecked",
        CitationState.Unstable => "unstable",
        CitationState.ScrubBlocked => "scrub-blocked",
        _ => "unknown",
    };

    private static string Word(SourceState state) => state switch
    {
        SourceState.Ok => "ok",
        SourceState.Degraded => "degraded",
        SourceState.Unavailable => "unavailable",
        _ => "unknown",
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
