using System.Globalization;
using System.Text;
using Aw.Context.Core.Graph;

namespace Aw.Context.Testing.Map;

/// <summary>A graph as one order-independent text, so two graphs compare by content.</summary>
public static class Canonical
{
    /// <summary>The text.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>One line per row, sorted.</returns>
    public static string Of(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        static string Evidence(IReadOnlyList<EvidenceRef> e) => string.Join('|', e.Select(r => $"{r.Key.Value}#{r.Span}"));
        static string Time(DateTimeOffset? t) => t?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "-";
        var lines = graph.Nodes.Select(n => string.Create(CultureInfo.InvariantCulture,
                $"node {n.Key.Value} '{n.Label}' {n.Provenance} {n.Confidence} [{Evidence(n.EvidenceOrEmpty)}] {n.Attributes?.GetRawText()} {Time(n.At)}"))
            .Concat(graph.Edges.Select(e => string.Create(CultureInfo.InvariantCulture,
                $"edge {e.From.Value} {e.Type} {e.To.Value} {e.Provenance} {e.Confidence} [{Evidence(e.EvidenceOrEmpty)}] {e.Order} {Time(e.At)}")))
            .Concat(graph.Aliases.Select(a => $"alias {a.Alias.Value} {a.Target.Value}"))
            .Concat(graph.Decisions.Select(d => $"decision {d.ToKey().Value} {d.Record.Value} '{d.Quote}' '{d.Wording}' {d.Provenance} {Time(d.At)}"))
            .Order(StringComparer.Ordinal);
        return new StringBuilder().AppendJoin('\n', lines).ToString();
    }
}
