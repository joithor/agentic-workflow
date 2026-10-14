namespace Aw.Context.Core.Graph;

/// <summary>
/// Runs key migrations over a graph (spec §6.1): every node, edge end, decision link and alias target moves to the new
/// key, and each changed node keeps its old key as an alias, so overrides and links that name it still resolve.
/// </summary>
public static class KeyMigrations
{
    /// <summary>The migrations this build ships: none yet (key version 1 is the first).</summary>
    public static IReadOnlyList<IKeyMigration> Shipped { get; } = [];

    /// <summary>Brings <paramref name="graph"/> from key version <paramref name="from"/> to the last version the chain reaches.</summary>
    /// <param name="graph">The graph, keyed at <paramref name="from"/>.</param>
    /// <param name="from">Its key version.</param>
    /// <param name="chain">The migrations, in any order; each step From → From + 1 is looked up.</param>
    /// <returns>The migrated graph and the key version it ends at.</returns>
    public static (GraphSnapshot Graph, int Version) Apply(GraphSnapshot graph, int from, IReadOnlyList<IKeyMigration> chain)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(chain);
        var version = from;
        while (chain.FirstOrDefault(m => m.From == version) is { } step)
        {
            graph = Step(graph, step);
            version = step.To;
        }

        return (graph, version);
    }

    private static GraphSnapshot Step(GraphSnapshot graph, IKeyMigration step)
    {
        NodeKey Map(NodeKey key) => key.Kind == NodeKind.Decision ? key : step.Migrate(key);

        var renamed = graph.Nodes.Where(n => Map(n.Key).Value != n.Key.Value).Select(n => new NodeAlias(n.Key, Map(n.Key)));
        var aliases = graph.Aliases.Select(a => a with { Target = Map(a.Target) }).Concat(renamed)
            .DistinctBy(a => a.Alias.Value, StringComparer.Ordinal);
        return new GraphSnapshot(
            [.. graph.Nodes.Select(n => n with { Key = Map(n.Key) })],
            [.. graph.Edges.Select(e => e with { From = Map(e.From), To = Map(e.To) })],
            [.. aliases],
            graph.Decisions);
    }
}
