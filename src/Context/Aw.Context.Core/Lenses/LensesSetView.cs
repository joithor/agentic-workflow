using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>
/// 'context lenses &lt;anchor&gt; &lt;anchor&gt;…' (scoping spec C1, schema 2): the union of several anchors' bands. A node several anchors reach appears once, in the first list that holds it
/// (own, then neighbors, then affects) with the minimum depth; its 'anchors' lists every anchor that reached it.
/// </summary>
/// <param name="SchemaVersion">2.</param>
/// <param name="Anchors">The anchors that resolved, in request order.</param>
/// <param name="MissingAnchors">The values (as given) that named no node this caller may read, in request order; one answer for every reason.</param>
/// <param name="MapVersion">The map read ('v13', or 'v13@7c3e1a2' when it was built at a commit).</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Depth">How far the neighbors reach and the cascade ran, 0 to 4.</param>
/// <param name="AsOf">When the view was built.</param>
/// <param name="Bands">The three bands over the union.</param>
/// <param name="Edges">Edges across the bands, over the union: mentions, decides, quotes, supersedes, implements and tests. 'bands.product.edges' holds the other types (part-of, on, reads, writes, step-of, depends-on, moves-to) among the product items, so no edge is in both: a full drawing uses both lists.</param>
/// <param name="Conflicts">Typed disagreements, de-duplicated.</param>
/// <param name="Coverage">Counts over the union of the anchors' own scopes, with 'byAnchor' for each.</param>
/// <param name="Searched">What was searched.</param>
/// <param name="Degraded">Sources that couldn't be read.</param>
/// <param name="Cites">The citations by marker.</param>
public sealed record LensesSetView(
    int SchemaVersion,
    IReadOnlyList<LensAnchor> Anchors,
    IReadOnlyList<string> MissingAnchors,
    string MapVersion,
    bool Draft,
    int Depth,
    DateTimeOffset AsOf,
    LensBands Bands,
    IReadOnlyList<LensEdge> Edges,
    IReadOnlyList<LensConflict> Conflicts,
    LensCoverage Coverage,
    LensSearched Searched,
    IReadOnlyList<string> Degraded,
    IReadOnlyDictionary<string, Citation> Cites);
