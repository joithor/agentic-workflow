using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Checks a map file and turns it into a <see cref="MapImport"/> (spec Phase 3), all or nothing: SND-CONTEXT-031 for a
/// shape problem (schema version, an empty entry, an unknown surface kind, a duplicate key or alias, a disallowed or
/// self edge, an authored 'quotes' edge), SND-CONTEXT-032 for a reference to a node the file doesn't define, each with
/// its key path. Every key (node, edge end, alias, override target) passes through EntryKey, or through the key
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
        Entries(document, problems);
        if (problems.Any)
        {
            return problems.ToError();
        }

        var current = document.KeyVersion == keyVersion;
        var graph = Keys(document, migrations, keyVersion, problems);
        var (withDecisions, quotes) = WithDecisions(graph, problems);
        var edges = MapGraphChecks.Run(withDecisions, problems);
        var overrides = current ? [.. (document.Overrides ?? []).Select(Normalized)] : document.Overrides ?? [];
        return problems.Any
            ? problems.ToError()
            : new MapImport(document.Repo, document.Commit, withDecisions with { Edges = [.. edges, .. quotes] }, overrides);
    }

    private static void Header(MapDocument document, int keyVersion, MapProblems problems)
    {
        if (!SchemaVersions.Contains(document.SchemaVersion))
        {
            problems.Invalid("schemaVersion", Fmt($"{document.SchemaVersion} isn't supported (this build reads {string.Join(", ", SchemaVersions)})"));
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

    /// <summary>A list may hold a null entry ('- ' with nothing after it in YAML); each is reported, not dereferenced.</summary>
    private static void Entries(MapDocument document, MapProblems problems)
    {
        Each("nodes", document.Nodes, problems);
        Each("edges", document.Edges, problems);
        Each("aliases", document.Aliases, problems);
        Each("decisions", document.Decisions, problems);
        Each("overrides", document.Overrides, problems);
    }

    private static void Each<T>(string name, IReadOnlyList<T?>? items, MapProblems problems)
        where T : class
    {
        for (var i = 0; i < (items?.Count ?? 0); i++)
        {
            if (items![i] is null)   // why: the loop runs only when items is not null
            {
                problems.Invalid(Fmt($"{name}[{i}]"), "an entry is required");
            }
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
                [.. graph.Aliases.Select(a => new NodeAlias(EntryKey.For(a.Alias), EntryKey.For(a.Target)))],
                graph.Decisions);
        }

        var (migrated, reached) = KeyMigrations.Apply(graph, document.KeyVersion, migrations);
        if (reached != keyVersion)
        {
            problems.Invalid("keyVersion", Fmt($"keys of version {document.KeyVersion} can't be migrated to {keyVersion}"));
        }

        return migrated;
    }

    /// <summary>Adds each decision's node, its record node (when the file lacks it) and its implied 'quotes' edge, kept apart from the file's edges.</summary>
    private static (GraphSnapshot Graph, List<GraphEdge> Quotes) WithDecisions(GraphSnapshot graph, MapProblems problems)
    {
        var nodes = graph.Nodes.ToList();
        var keys = nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var quotes = new List<GraphEdge>();
        for (var i = 0; i < graph.Decisions.Count; i++)
        {
            var d = graph.Decisions[i];
            if (!NodeKey.TryFrom($"record:{d.Record.Value}", out var record))
            {
                problems.Invalid(Fmt($"decisions[{i}].record"), "too long to be pointed at by a record node (at most 1017 characters)");
                continue;
            }

            nodes.Add(new GraphNode(d.ToKey(), d.Wording, d.Provenance, Evidence: [new EvidenceRef(d.Record)], At: d.At));
            if (keys.Add(record.Value))
            {
                nodes.Add(new GraphNode(record, d.Record.Value, Provenance.Deterministic, At: d.At));
            }

            quotes.Add(new GraphEdge(d.ToKey(), record, EdgeType.Quotes, Provenance.Deterministic, At: d.At));
        }

        return (graph with { Nodes = nodes }, quotes);
    }

    private static MapOverride Normalized(MapOverride o) =>
        o with { Target = EntryKey.For(o.Target), Other = o.Other is { } other ? EntryKey.For(other) : null };

    private static string Fmt(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
