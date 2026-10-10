using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>No namespace cycles inside a project (spec §4b.1a; MSBuild already refuses project cycles).</summary>
public static class CycleRules
{
    /// <summary>Two or more namespaces of one assembly depend on each other.</summary>
    public const string Id = "ARCH-CYCLE-001";

    /// <summary>Checks the namespace graph of each assembly for cycles.</summary>
    /// <param name="types">The types to check (their dependencies on types outside the set are ignored).</param>
    /// <returns>One violation per cycle (a strongly connected set of namespaces).</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types) =>
        types.GroupBy(TypeNames.AssemblyOf, StringComparer.Ordinal)
            .SelectMany(g => Cycles(Graph([.. g])).Select(c => new Violation(Id,
                $"namespaces {string.Join(" <-> ", c)} depend on each other in {g.Key}",
                "break the cycle: move the shared types into the lower namespace, or invert one dependency through a port")));

    private static Dictionary<string, HashSet<string>> Graph(List<IType> types)
    {
        var inSet = types.Select(TypeNames.NamespaceOf).ToHashSet(StringComparer.Ordinal);
        var graph = inSet.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (from, to) in types.SelectMany(t => t.Dependencies
            .Where(d => d.Target is not GenericParameter)
            .Select(d => (TypeNames.NamespaceOf(t), TypeNames.NamespaceOf(d.Target)))))
        {
            // The global namespace (Program, <PrivateImplementationDetails>) isn't a feature folder; it joins no cycle.
            if (from != to && from.Length > 0 && to.Length > 0 && inSet.Contains(to))
            {
                _ = graph[from].Add(to);
            }
        }

        return graph;
    }

    // Tarjan's strongly connected components; every component with more than one namespace is a cycle.
    private static IEnumerable<IReadOnlyList<string>> Cycles(Dictionary<string, HashSet<string>> graph)
    {
        var state = new Tarjan(graph);
        foreach (var node in graph.Keys.Order(StringComparer.Ordinal))
        {
            if (!state.Index.ContainsKey(node))
            {
                state.Visit(node);
            }
        }

        return state.Components.Where(c => c.Count > 1);
    }
}
