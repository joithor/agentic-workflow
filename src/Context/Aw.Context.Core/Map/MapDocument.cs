using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>
/// A map file (spec Phase 3): what 'context map export' writes and 'context map import' reads, YAML or JSON. An export
/// holds nodes, edges with their evidence, aliases, decisions and overrides (rejections included) with their original
/// timestamps and provenance. Trust is never in the file: it is recomputed from provenance.
/// </summary>
/// <param name="SchemaVersion">The file format version (<see cref="MapDocumentValidator.SchemaVersions"/>).</param>
/// <param name="Kind">Authored or export.</param>
/// <param name="Repo">The repo the map describes.</param>
/// <param name="Nodes">The nodes.</param>
/// <param name="Edges">The edges, or null.</param>
/// <param name="Aliases">Other keys for nodes, or null.</param>
/// <param name="Decisions">Decisions; each one's node and its 'quotes' edge are implied, or null.</param>
/// <param name="Overrides">The override layer, or null.</param>
/// <param name="KeyVersion">The EntryKey version the keys were written with; older keys are migrated on import.</param>
/// <param name="Commit">The commit the map describes, or null.</param>
/// <param name="MapVersion">The version it was exported from, or null.</param>
/// <param name="ExportedAt">When it was exported, or null.</param>
public sealed record MapDocument(
    int SchemaVersion,
    MapDocumentKind Kind,
    string Repo,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge>? Edges = null,
    IReadOnlyList<NodeAlias>? Aliases = null,
    IReadOnlyList<DecisionRow>? Decisions = null,
    IReadOnlyList<MapOverride>? Overrides = null,
    int KeyVersion = EntryKey.Version,
    string? Commit = null,
    int? MapVersion = null,
    DateTimeOffset? ExportedAt = null);
