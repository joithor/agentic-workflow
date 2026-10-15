using System.Globalization;
using System.Text;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Sindri.Host.Text;

/// <summary>
/// The text form of 'context show', 'neighbors' and 'path' (context spec §8.4 "Text output"): a header naming the map
/// version, then one line per node or edge. States are words ('draft' for an unconfirmed node or edge, 'untrusted');
/// there is never colour, so NO_COLOR holds. Empty answers are worded.
/// </summary>
internal static class ContextGraphText
{
    internal static string Node(NodeView view)
    {
        var text = new StringBuilder(Invariant($"node {view.Node.Key.Value} · map {view.MapVersion}\n"));
        _ = text.AppendLine(Invariant($"  {view.Node.Label} · {Kind(view.Node.Key)} · {Facts(view.Node)}"));
        _ = text.AppendLine(view.Aliases.Count == 0 ? "aliases: none" : "aliases: " + string.Join(", ", view.Aliases.Select(a => a.Value)));
        Edges(text, "out", view.Out, e => Invariant($"{EdgeRules.Name(e.Type)} {e.To.Value}"));
        Edges(text, "in", view.In, e => Invariant($"{e.From.Value} {EdgeRules.Name(e.Type)} this"));
        return text.ToString();
    }

    internal static string Neighbors(NeighborsView view)
    {
        var text = new StringBuilder(Invariant($"neighbors of {view.Center.Value} within {view.Depth} edge(s) · map {view.MapVersion}\n"));
        if (view.Nodes.Count <= 1)
        {
            return text.AppendLine(Invariant($"no neighbors within {view.Depth} edge(s)")).ToString();
        }

        foreach (var n in view.Nodes.Skip(1))
        {
            _ = text.AppendLine(Invariant($"  depth {n.Depth}  {n.Node.Key.Value}  {n.Node.Label}{Draft(n.Node.Confirmed)}"));
        }

        Edges(text, "edges", view.Edges, e => Invariant($"{e.From.Value} {EdgeRules.Name(e.Type)} {e.To.Value}"));
        return text.ToString();
    }

    internal static string Path(PathView view)
    {
        var text = new StringBuilder(Invariant($"path {view.From.Value} → {view.To.Value} · map {view.MapVersion}\n"));
        if (!view.Found || view.Steps.Count == 0)
        {
            return text.AppendLine(view.Found ? "the same node: no steps" : Invariant($"no path within {GraphWalk.MaxPathLength} edges")).ToString();
        }

        var i = 0;
        foreach (var step in view.Steps)
        {
            var name = EdgeRules.Name(step.Edge.Type);
            var arrow = step.Forward ? name + " →" : "← " + name;
            _ = text.AppendLine(Invariant($"  {++i}. {arrow} {step.Node.Key.Value}  {step.Node.Label}{Draft(step.Edge.Confirmed && step.Node.Confirmed)}"));
        }

        return text.ToString();
    }

    private static void Edges(StringBuilder text, string title, IReadOnlyList<EdgeFact> edges, Func<EdgeFact, string> line)
    {
        _ = text.AppendLine(edges.Count == 0 ? title + ": none" : title + ":");
        foreach (var edge in edges)
        {
            _ = text.Append("  ").Append(line(edge)).AppendLine(Draft(edge.Confirmed));
        }
    }

    private static string Facts(NodeFact node) =>
        $"{Word(node.Provenance)}, {(node.Confirmed ? "confirmed" : "draft")}, {(node.Trust == Trust.Trusted ? "trusted" : "untrusted")}";

    private static string Draft(bool confirmed) => confirmed ? string.Empty : " (draft)";

    private static string Kind(NodeKey key) => key.Value[..key.Value.IndexOf(':', StringComparison.Ordinal)];

    private static string Word(Provenance provenance) => provenance switch
    {
        Provenance.Human => "human",
        Provenance.Deterministic => "deterministic",
        Provenance.Model => "model",
        _ => "unknown",
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
