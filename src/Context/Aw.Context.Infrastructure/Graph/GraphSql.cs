namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's SQL (tables in Store/Migrations/0002_s3a_graph.sql).</summary>
internal static class GraphSql
{
    public const string InsertVersion =
        "INSERT INTO map_version (repo, commit_sha, key_version, created, status) VALUES ($repo, $commit, $keyVersion, $created, 'building') RETURNING id";

    public const string InsertNode =
        "INSERT INTO node (map_version, key, label, attributes, provenance, confidence, trust, written_at) VALUES ($v, $key, $label, $attributes, $provenance, $confidence, $trust, $at)";

    public const string InsertNodeEvidence =
        "INSERT INTO node_evidence (map_version, node_key, position, record, span) VALUES ($v, $key, $position, $record, $span)";

    public const string InsertAlias = "INSERT INTO node_alias (map_version, alias, target) VALUES ($v, $alias, $target)";

    public const string InsertEdge =
        "INSERT INTO edge (map_version, from_key, to_key, type, provenance, confidence, trust, step_order, written_at) VALUES ($v, $from, $to, $type, $provenance, $confidence, $trust, $order, $at)";

    public const string InsertEdgeEvidence =
        "INSERT INTO edge_evidence (edge_id, position, record, span) SELECT id, $position, $record, $span FROM edge WHERE map_version = $v AND from_key = $from AND to_key = $to AND type = $type";

    public const string InsertDecision =
        "INSERT INTO decision (map_version, key, quote, wording, provenance, trust, written_at) VALUES ($v, $key, $quote, $wording, $provenance, $trust, $at)";

    public const string InsertOverride =
        "INSERT INTO map_override (id, action, target, label, other, who, at, rejection) VALUES ($id, $action, $target, $label, $other, $who, $at, $rejection) ON CONFLICT (id) DO NOTHING";

    public const string DeleteBuilding = "DELETE FROM map_version WHERE status = 'building'";

    public const string CountGraphTables = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'map_version'";

    public const string RetireCurrent = "UPDATE map_version SET status = 'retained' WHERE status = 'current'";

    public const string IsBuilding = "SELECT count(*) FROM map_version WHERE id = $v AND status = 'building'";

    public const string MakeCurrent = "UPDATE map_version SET status = 'current' WHERE id = $v AND status = 'building'";

    public const string DeleteRetained = "DELETE FROM map_version WHERE id = $v AND status = 'retained'";

    public const string DeleteBuildingVersion = "DELETE FROM map_version WHERE id = $v AND status = 'building'";

    public const string SelectVersions =
        "SELECT id, repo, commit_sha, key_version, created, status FROM map_version WHERE status <> 'building' ORDER BY id DESC";

    public const string SelectNodes =
        "SELECT key, label, attributes, provenance, confidence, written_at FROM node WHERE map_version = $v ORDER BY key";

    public const string SelectNodeEvidence =
        "SELECT node_key, record, span FROM node_evidence WHERE map_version = $v ORDER BY node_key, position";

    public const string SelectEdges =
        "SELECT id, from_key, to_key, type, provenance, confidence, step_order, written_at FROM edge WHERE map_version = $v ORDER BY id";

    public const string SelectEdgeEvidence =
        "SELECT ee.edge_id, ee.record, ee.span FROM edge_evidence ee JOIN edge e ON e.id = ee.edge_id WHERE e.map_version = $v ORDER BY ee.edge_id, ee.position";

    public const string SelectAliases = "SELECT alias, target FROM node_alias WHERE map_version = $v ORDER BY alias";

    // The quoted record lives on the decision's 'quotes' edge ('record:' + canonical key), not in the decision row.
    public const string SelectDecisions =
        "SELECT substr(e.to_key, 8), d.quote, d.wording, d.provenance, d.written_at FROM decision d " +
        "JOIN edge e ON e.map_version = d.map_version AND e.from_key = d.key AND e.type = 'quotes' WHERE d.map_version = $v ORDER BY d.key";

    public const string SelectOverrides =
        "SELECT action, target, who, at, label, other, rejection FROM map_override ORDER BY at, id";
}
