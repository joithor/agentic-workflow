using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>Reads one map version's graph: nodes, edges and decisions with their evidence and aliases.</summary>
internal static class GraphReads
{
    public static async ValueTask<Result<GraphSnapshot>> Version(ISqliteSession session, int version, CancellationToken cancellationToken)
    {
        SqlStatement Of(string sql) => new(sql, c => c.Bind("$v", version));

        var nodes = await SqliteQueries.List(session, Of(GraphSql.SelectNodes), GraphRows.Node, cancellationToken).ConfigureAwait(false);
        var nodeEvidence = await SqliteQueries.List(session, Of(GraphSql.SelectNodeEvidence), GraphRows.Evidence, cancellationToken).ConfigureAwait(false);
        var edges = await SqliteQueries.List(session, Of(GraphSql.SelectEdges), GraphRows.Edge, cancellationToken).ConfigureAwait(false);
        var edgeEvidence = await SqliteQueries.List(session, Of(GraphSql.SelectEdgeEvidence), GraphRows.Evidence, cancellationToken).ConfigureAwait(false);
        var aliases = await SqliteQueries.List(session, Of(GraphSql.SelectAliases), GraphRows.Alias, cancellationToken).ConfigureAwait(false);
        var decisions = await SqliteQueries.List(session, Of(GraphSql.SelectDecisions), GraphRows.Decision, cancellationToken).ConfigureAwait(false);
        if (new[] { Fail(nodes), Fail(nodeEvidence), Fail(edges), Fail(edgeEvidence), Fail(aliases), Fail(decisions) }.FirstOrDefault(e => e is not null) is { } error)
        {
            return error;
        }

        var byNode = Group(nodeEvidence.Value);
        var byEdge = Group(edgeEvidence.Value);
        return new GraphSnapshot(
            [.. nodes.Value.Select(n => byNode.TryGetValue(n.Key.Value, out var e) ? n with { Evidence = e } : n)],
            [.. edges.Value.Select(r => byEdge.TryGetValue(r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), out var e) ? r.Edge with { Evidence = e } : r.Edge)],
            aliases.Value,
            decisions.Value);
    }

    private static Dictionary<string, IReadOnlyList<EvidenceRef>> Group(IReadOnlyList<(string Owner, EvidenceRef Evidence)> rows) =>
        rows.GroupBy(r => r.Owner, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<EvidenceRef>)[.. g.Select(r => r.Evidence)], StringComparer.Ordinal);

    private static Error? Fail<T>(Result<T> result) => result.IsError ? result.Error : null;
}
