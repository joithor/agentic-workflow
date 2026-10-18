using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>
/// The component hop (SC App. C, C4): a node, a confirmed code unit that implements it, another node that unit implements. Only
/// confirmed 'implements' edges count, and a unit that implements more than the cap is skipped (a shared utility would relate
/// everything). A unit is reported when it is cut at expansion and at least one node it would reach is not yet visited, so a unit whose other features were already reached through nearer steps is not reported. Pure.
/// </summary>
internal static class ComponentHop
{
    internal static IEnumerable<Cascade.Step> From(GraphIndex index, NodeKey node, int cap, HashSet<string> visited, Dictionary<string, int> skipped)
    {
        foreach (var near in index.In(node, EdgeType.Implements).Where(Confirmed))
        {
            var far = index.Out(near.From, EdgeType.Implements).Where(e => Confirmed(e) && e.To.Value != node.Value).ToList();
            var features = far.Select(e => Cascade.Ascend(index, e.To).Value).Append(Cascade.Ascend(index, node).Value).Distinct(StringComparer.Ordinal).Count();
            if (features <= cap)
            {
                foreach (var e in far)
                {
                    yield return new Cascade.Step(e.To, [$"component {near.From.Value}", $"implements {e.To.Value}"], [near, e]);
                }
            }
            else if (far.Exists(e => !visited.Contains(e.To.Value)))
            {
                skipped[near.From.Value] = features;
            }
        }
    }

    private static bool Confirmed(GraphEdge edge) => edge.Provenance != Provenance.Model;
}
