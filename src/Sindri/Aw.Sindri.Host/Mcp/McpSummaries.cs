using System.Globalization;
using System.Text;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The text block beside a tool's structuredContent (context spec §8.5): anchors, counts, conflicts, degraded sources and
/// the citations' keys. Never record text: the model reads that, fenced, from structuredContent.
/// </summary>
internal static class McpSummaries
{
    /// <summary>context_query's summary.</summary>
    /// <param name="result">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Query(ContextResult result)
    {
        var items = result.Sections.Sum(s => s.Items.Count);
        var degraded = result.Degraded.Count == 0 ? "none" : string.Join(", ", result.Degraded.Select(d => d.Source.Value));
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"{items} items in {result.Sections.Count} sections; {result.Conflicts.Count} conflicts; degraded: {degraded}"))
            .AppendLine(result.Truncated ? "; truncated to the budget" : string.Empty)
            .AppendLine($"anchors: {(result.Anchors.Count == 0 ? "none" : string.Join(", ", result.Anchors))}");
        if (result.Diff is { } diff)
        {
            var c = diff.Counts;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"diff: {c.Changed} changed: {c.Mapped} mapped, {c.Suggested} suggested, {c.Unmapped} unmapped, {c.Denied} denied{(diff.Truncated ? " (file list truncated)" : string.Empty)}"));
        }

        if (result.SuggestedAnchors.Count > 0)
        {
            _ = text.AppendLine($"suggested anchors: {string.Join(", ", result.SuggestedAnchors)}");
        }

        foreach (var (marker, cite) in result.Cites)
        {
            _ = text.AppendLine($"{marker} {cite.Key.Value} ({Word(cite.State)})");
        }

        return text.Append("The records, their text fenced as untrusted, are in structuredContent; cite them as [Cn].").ToString();
    }

    /// <summary>context_lenses' summary: the band sizes, the conflicts and the coverage line; never record text.</summary>
    /// <param name="view">The lenses.</param>
    /// <returns>The text.</returns>
    internal static string Lenses(LensesView view)
    {
        var bands = view.Bands;
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture,
            $"lenses of {view.Anchor.Key.Value} · map {view.MapVersion}: intent {bands.Intent.Counts.Records} records and {bands.Intent.Counts.Decisions} decisions, product {bands.Product.Counts.Own} own and {bands.Product.Counts.Affects} affected, code {bands.Code.Counts.Files} files\n"));
        _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{view.Conflicts.Count} conflicts{(view.Conflicts.Count == 0 ? string.Empty : ": " + string.Join(", ", view.Conflicts.Select(c => c.Kind.ToString().ToLowerInvariant())))}"));
        var c = view.Coverage;
        _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"coverage: {c.Intent.Covered} of {c.Scope} in scope have a record or decision, {c.Product.Confirmed} confirmed, {c.Code.Implemented} implemented in code ({c.Code.Suggested} suggested), {c.Gaps.Count} gaps"));
        _ = text.AppendLine(view.Degraded.Count == 0 ? "degraded: none" : "degraded: " + string.Join(", ", view.Degraded));
        return text.Append("The bands, their text fenced as untrusted, are in structuredContent.").ToString();
    }

    /// <summary>context_node's summary: the key, the map version and the edge counts; never a label.</summary>
    /// <param name="view">The node.</param>
    /// <returns>The text.</returns>
    internal static string Node(NodeView view) => string.Create(CultureInfo.InvariantCulture,
        $"node {view.Node.Key.Value} · map {view.MapVersion}: {view.Out.Count} out and {view.In.Count} in edges\nThe node and its edges, their labels fenced as untrusted, are in structuredContent.");

    /// <summary>context_neighbors' summary: the centre, the depth and the counts; never a label.</summary>
    /// <param name="view">The neighbors.</param>
    /// <returns>The text.</returns>
    internal static string Neighbors(NeighborsView view) => string.Create(CultureInfo.InvariantCulture,
        $"neighbors of {view.Center.Value} · map {view.MapVersion} · depth {view.Depth}: {view.Nodes.Count} nodes and {view.Edges.Count} edges\nThe nodes and edges, their labels fenced as untrusted, are in structuredContent.");

    /// <summary>context_sources' summary: which instance answered, then the sources table the CLI prints.</summary>
    /// <param name="view">The sources.</param>
    /// <param name="build">This build's version.</param>
    /// <param name="port">The port the call arrived on.</param>
    /// <returns>The text.</returns>
    internal static string Sources(SourcesView view, string build, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"sindri {build}, port {port}\n") + ContextText.Render(view);

    private static string Word(CitationState state) => state switch
    {
        CitationState.Current => "current",
        CitationState.Stale => "stale",
        CitationState.Unchecked => "unchecked",
        CitationState.Unstable => "unstable",
        CitationState.ScrubBlocked => "scrub-blocked",
        _ => "unknown",
    };
}
