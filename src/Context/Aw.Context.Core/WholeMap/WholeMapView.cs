using System.Text.Json.Serialization;

namespace Aw.Context.Core.WholeMap;

/// <summary>'GET /sindri/context/map/whole' (Part D, §2.1): the whole map at one of two levels of detail. The areas and the edges between them are always there, so a page can draw the overview from any answer.</summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="MapCommit">The commit the map was built at (first seven characters), or null for an imported map.</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the map version was written.</param>
/// <param name="Repo">The repo this answer is for, or null when the profile has several and the question named none.</param>
/// <param name="Detail">'nodes' or 'areas', what the answer carries.</param>
/// <param name="Fallback">'over-budget' when areas answer a question that wanted nodes, else null.</param>
/// <param name="Counts">What the map holds.</param>
/// <param name="Areas">Every area, sorted by key.</param>
/// <param name="AreaEdges">The edges between areas, aggregated by source, target and type.</param>
/// <param name="Nodes">Every node (detail 'nodes') or the opened area's nodes, else null.</param>
/// <param name="Edges">Every edge between the returned nodes, else null.</param>
/// <param name="Area">The opened area, or null.</param>
/// <param name="Located">The area of each asked-for key, or null when none was asked.</param>
/// <param name="Truncated">True when a list was cut at its cap.</param>
public sealed record WholeMapView(
    int SchemaVersion,
    string MapVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MapCommit,
    bool Draft,
    DateTimeOffset AsOf,
    string? Repo,
    WholeMapDetail Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Fallback,
    WholeMapCounts Counts,
    IReadOnlyList<WholeMapArea> Areas,
    IReadOnlyList<WholeMapAreaEdge> AreaEdges,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WholeMapNode>? Nodes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WholeMapEdge>? Edges,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WholeMapAreaInfo? Area,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WholeMapLocated>? Located,
    bool Truncated);
