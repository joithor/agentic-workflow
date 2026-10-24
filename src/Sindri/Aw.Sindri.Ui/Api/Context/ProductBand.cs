namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The product lens: the anchors' own nodes, their neighbors and what they affect.</summary>
/// <param name="Own">Own, as served.</param>
/// <param name="Neighbors">Neighbors, as served.</param>
/// <param name="Affects">Affects, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="Edges">Edges, as served.</param>
internal sealed record ProductBand(
    IReadOnlyList<ProductNode> Own,
    IReadOnlyList<ProductNode> Neighbors,
    IReadOnlyList<ProductNode> Affects,
    ProductCounts Counts,
    IReadOnlyList<LensEdge> Edges);
