using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Sources;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's row mappers, one per SELECT in <see cref="GraphSql"/>.</summary>
internal static class GraphRows
{
    public static readonly IRowMapper<long> Id = new IdMapper();
    public static readonly IRowMapper<MapVersion> Version = new VersionMapper();
    public static readonly IRowMapper<GraphNode> Node = new NodeMapper();
    public static readonly IRowMapper<(string Owner, EvidenceRef Evidence)> Evidence = new EvidenceMapper();
    public static readonly IRowMapper<(long Id, GraphEdge Edge)> Edge = new EdgeMapper();
    public static readonly IRowMapper<NodeAlias> Alias = new AliasMapper();
    public static readonly IRowMapper<DecisionRow> Decision = new DecisionMapper();
    public static readonly IRowMapper<MapOverride> Override = new OverrideMapper();

    private static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private sealed class IdMapper : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class VersionMapper : IRowMapper<MapVersion>
    {
        public MapVersion Map(SqliteDataReader r) => new(r.GetInt32(0) is > 0 and var number ? number : throw new InvalidOperationException("map_version.id is not positive"), r.GetString(1), Text(r, 2), r.GetInt32(3),
            GraphText.Time(r.GetString(4)) ?? default, GraphText.Status(r.GetString(5)));
    }

    private sealed class NodeMapper : IRowMapper<GraphNode>
    {
        public GraphNode Map(SqliteDataReader r)
        {
            JsonElement? attributes = null;
            if (Text(r, 2) is { } json)
            {
                using var document = JsonDocument.Parse(json);
                attributes = document.RootElement.Clone();
            }

            return new GraphNode(NodeKey.From(r.GetString(0)), r.GetString(1), GraphText.Provenance(r.GetString(3)), r.GetDouble(4),
                Evidence: null, Attributes: attributes, At: GraphText.Time(Text(r, 5)));
        }
    }

    private sealed class EvidenceMapper : IRowMapper<(string, EvidenceRef)>
    {
        public (string, EvidenceRef) Map(SqliteDataReader r) =>
            (r.IsDBNull(0) ? string.Empty : Convert.ToString(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!,   // why: a non-null column converts to a non-null string
             new EvidenceRef(RecordKey.From(r.GetString(1)), Text(r, 2)));
    }

    private sealed class EdgeMapper : IRowMapper<(long, GraphEdge)>
    {
        public (long, GraphEdge) Map(SqliteDataReader r) => (r.GetInt64(0), new GraphEdge(
            NodeKey.From(r.GetString(1)), NodeKey.From(r.GetString(2)), GraphText.EdgeType(r.GetString(3)), GraphText.Provenance(r.GetString(4)),
            r.GetDouble(5), Evidence: null, Order: r.IsDBNull(6) ? null : r.GetInt32(6), At: GraphText.Time(Text(r, 7))));
    }

    private sealed class AliasMapper : IRowMapper<NodeAlias>
    {
        public NodeAlias Map(SqliteDataReader r) => new(NodeKey.From(r.GetString(0)), NodeKey.From(r.GetString(1)));
    }

    private sealed class DecisionMapper : IRowMapper<DecisionRow>
    {
        public DecisionRow Map(SqliteDataReader r) => new(RecordKey.From(r.GetString(0)), r.GetString(1), r.GetString(2),
            GraphText.Provenance(r.GetString(3)), GraphText.Time(Text(r, 4)));
    }

    private sealed class OverrideMapper : IRowMapper<MapOverride>
    {
        public MapOverride Map(SqliteDataReader r) => new(GraphText.Action(r.GetString(0)), NodeKey.From(r.GetString(1)), r.GetString(2),
            GraphText.Time(r.GetString(3)) ?? default, Text(r, 4), Text(r, 5) is { } other ? NodeKey.From(other) : null, Text(r, 6));
    }
}
