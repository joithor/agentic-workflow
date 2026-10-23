using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

internal static partial class RunGraphBuilder
{
    // The engine serves the recipe's transitions: each (from, to) pair between two drawn cards is one edge, labelled by the first label served for it.
    // An edge to an end is an end edge, one that goes back up the path is a loop, and every other is the main way on.
    private static List<RunEdge> FromTransitions(List<RunNode> nodes, IReadOnlyList<RecipeTransitionView> transitions)
    {
        var byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var edges = new List<RunEdge>();
        foreach (var transition in transitions)
        {
            foreach (var from in transition.From)
            {
                if (byId.TryGetValue(from, out var source) && byId.TryGetValue(transition.To, out var target))
                {
                    AddTransition(edges, source, target, transition);
                }
            }
        }

        return edges;
    }

    private static void AddTransition(List<RunEdge> edges, RunNode source, RunNode target, RecipeTransitionView transition)
    {
        var key = source.Id + ">" + target.Id;
        var at = edges.FindIndex(e => string.Equals(e.Key, key, StringComparison.Ordinal));
        if (at >= 0)
        {
            if (edges[at].Label is null && transition.Label is { Length: > 0 } label)
            {
                edges[at] = edges[at] with { Label = label };
            }

            return;
        }

        var kind = target.Kind == RunNodeKind.End ? RunEdgeKind.End : GoesBack(source, target) ? RunEdgeKind.Loop : RunEdgeKind.Main;
        var tone = kind switch
        {
            RunEdgeKind.End => source.State == RunNodeState.Failed ? EdgeTone.Failed : EdgeTone.Normal,
            RunEdgeKind.Loop => EdgeTone.Dim,
            RunEdgeKind.Main => MainTone(source.State, target.State),
            _ => EdgeTone.Normal,
        };
        edges.Add(new RunEdge(source.Id, target.Id, kind, kind == RunEdgeKind.End ? target.Outcome : null, transition.Label is { Length: > 0 } text ? text : null, tone));
    }

    // Up the path: the target comes at or before the source on the main path.
    private static bool GoesBack(RunNode source, RunNode target) => source.Order is { } from && target.Order is { } to && target.Kind == RunNodeKind.Stage && to <= from;
}
