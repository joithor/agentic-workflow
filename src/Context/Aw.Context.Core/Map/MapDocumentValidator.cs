using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Checks a map file and turns it into a <see cref="MapImport"/> (spec Phase 3), all or nothing: SND-CONTEXT-031 for a
/// shape problem (schema version, an unknown surface kind, a duplicate key, a disallowed edge), SND-CONTEXT-032 for a
/// reference to a node the file doesn't define, each with its key path. Keys pass through EntryKey, or through the key
/// migrations when the file was written with an older key version. Overrides may name nodes the file lacks: they are
/// kept, as orphaned overrides are (spec §6.1).
/// </summary>
public static class MapDocumentValidator
{
    /// <summary>The file format versions this build reads: the current one (1) and, once there is one, the previous.</summary>
    public static IReadOnlyList<int> SchemaVersions { get; } = [1];

    /// <summary>Checks <paramref name="document"/>.</summary>
    /// <param name="document">The parsed file.</param>
    /// <param name="migrations">The key migrations (KeyMigrations.Shipped in production).</param>
    /// <param name="keyVersion">The key version to bring keys to (EntryKey.Version in production).</param>
    /// <returns>The import, or SND-CONTEXT-031/032.</returns>
    public static Result<MapImport> Check(MapDocument document, IReadOnlyList<IKeyMigration> migrations, int keyVersion = EntryKey.Version)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problems = new MapProblems();
        Header(document, keyVersion, problems);
        if (problems.Any)
        {
            return problems.ToError();
        }

        var graph = Keys(document, migrations, keyVersion, problems);
        graph = WithDecisions(graph);
        Nodes(graph, problems);
        var edges = Edges(graph, problems);
        Aliases(graph, problems);
        return problems.Any
            ? problems.ToError()
            : new MapImport(document.Repo, document.Commit, graph with { Edges = edges }, document.Overrides ?? []);
    }

    private static void Header(MapDocument document, int keyVersion, MapProblems problems)
    {
        if (!SchemaVersions.Contains(document.SchemaVersion))
        {
            problems.Invalid("schemaVersion", Fmt($"{document.SchemaVersion} isn't supported (this build reads {string.Join(", ", SchemaVersions)})"));
        }

        if (string.IsNullOrWhiteSpace(document.Repo))
        {
            problems.Invalid("repo", "the repo's short name is required");
        }

        if (document.MapVersion is <= 0)
        {
            problems.Invalid("mapVersion", Fmt($"{document.MapVersion} isn't a map version (1 or more)"));
        }

        if (document.KeyVersion < 1 || document.KeyVersion > keyVersion)
        {
            problems.Invalid("keyVersion", Fmt($"{document.KeyVersion} isn't supported (1 to {keyVersion})"));
        }
    }

    private static GraphSnapshot Keys(MapDocument document, IReadOnlyList<IKeyMigration> migrations, int keyVersion, MapProblems problems)
    {
        var graph = new GraphSnapshot(document.Nodes, document.Edges ?? [], document.Aliases ?? [], document.Decisions ?? []);
        if (document.KeyVersion == keyVersion)
        {
            return new GraphSnapshot(
                [.. graph.Nodes.Select(n => n with { Key = EntryKey.For(n.Key) })],
                [.. graph.Edges.Select(e => e with { From = EntryKey.For(e.From), To = EntryKey.For(e.To) })],
                [.. graph.Aliases.Select(a => a with { Target = EntryKey.For(a.Target) })],
                graph.Decisions);
        }

        var (migrated, reached) = KeyMigrations.Apply(graph, document.KeyVersion, migrations);
        if (reached != keyVersion)
        {
            problems.Invalid("keyVersion", Fmt($"keys of version {document.KeyVersion} can't be migrated to {keyVersion}"));
        }

        return migrated;
    }

    private static GraphSnapshot WithDecisions(GraphSnapshot graph)
    {
        var nodes = graph.Nodes.ToList();
        var edges = graph.Edges.ToList();
        foreach (var d in graph.Decisions)
        {
            var record = NodeKey.From($"record:{d.Record.Value}");
            nodes.Add(new GraphNode(d.Key, d.Wording, d.Provenance, Evidence: [new EvidenceRef(d.Record)], At: d.At));
            if (!nodes.Exists(n => n.Key.Value == record.Value))
            {
                nodes.Add(new GraphNode(record, d.Record.Value, Provenance.Deterministic, At: d.At));
            }

            edges.Add(new GraphEdge(d.Key, record, EdgeType.Quotes, Provenance.Deterministic, At: d.At));
        }

        return graph with { Nodes = nodes, Edges = edges };
    }

    private static void Nodes(GraphSnapshot graph, MapProblems problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var path = Fmt($"nodes[{i}]");
            if (!seen.Add(node.Key.Value))
            {
                problems.Invalid($"{path}.key", $"'{node.Key.Value}' is defined twice (or a decision in the file has the same key)");
            }

            if (!NodeKinds.HasValidSurfaceKind(node.Key))
            {
                problems.Invalid($"{path}.key", $"'{node.Key.Value}' needs a surface kind ({string.Join(", ", NodeKinds.SurfaceKinds.Order(StringComparer.Ordinal))}) and a name: 'surface:<kind>/<name>'");
            }

            if (string.IsNullOrWhiteSpace(node.Label))
            {
                problems.Invalid($"{path}.label", "a label is required");
            }

            Confidence(node.Confidence, $"{path}.confidence", problems);
        }
    }

    private static List<GraphEdge> Edges(GraphSnapshot graph, MapProblems problems)
    {
        var edges = new List<GraphEdge>();
        for (var i = 0; i < graph.Edges.Count; i++)
        {
            var edge = graph.Edges[i];
            var path = Fmt($"edges[{i}]");
            var from = Defined(graph, edge.From, $"{path}.from", problems);
            var to = Defined(graph, edge.To, $"{path}.to", problems);
            Confidence(edge.Confidence, $"{path}.confidence", problems);
            if (from is null || to is null)
            {
                continue;
            }

            if (!EdgeRules.Allows(edge.Type, from.Key.Kind, to.Key.Kind))
            {
                problems.Invalid($"{path}.type", $"'{EdgeRules.Name(edge.Type)}' doesn't go from a {Scheme(from.Key)} to a {Scheme(to.Key)} (spec §6.1's edge table)");
            }

            var resolved = edge with { From = from.Key, To = to.Key };
            if (!edges.Exists(e => e.SameFact(resolved)))
            {
                edges.Add(resolved);
            }
        }

        return edges;
    }

    private static void Aliases(GraphSnapshot graph, MapProblems problems)
    {
        for (var i = 0; i < graph.Aliases.Count; i++)
        {
            var alias = graph.Aliases[i];
            if (graph.Nodes.Any(n => n.Key.Value == alias.Alias.Value))
            {
                problems.Invalid(Fmt($"aliases[{i}].alias"), $"'{alias.Alias.Value}' is a node's own key");
            }
            else if (!graph.Nodes.Any(n => n.Key.Value == alias.Target.Value))
            {
                problems.Undefined(Fmt($"aliases[{i}].target"), alias.Target.Value);
            }
        }
    }

    private static GraphNode? Defined(GraphSnapshot graph, NodeKey key, string path, MapProblems problems)
    {
        var node = graph.Resolve(key);
        if (node is null)
        {
            problems.Undefined(path, key.Value);
        }

        return node;
    }

    private static void Confidence(double value, string path, MapProblems problems)
    {
        if (value is < 0 or > 1 || double.IsNaN(value))
        {
            problems.Invalid(path, Fmt($"{value} is outside 0 to 1"));
        }
    }

    private static string Scheme(NodeKey key) => key.Value[..key.Value.IndexOf(':', StringComparison.Ordinal)];

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
