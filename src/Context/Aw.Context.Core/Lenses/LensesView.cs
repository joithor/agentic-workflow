using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>'context lenses &lt;anchor&gt;' (spec §8.9): intent, product and code bands around one node, with the edges across them, their conflicts and coverage.</summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Anchor">The anchor node.</param>
/// <param name="MapVersion">The map read ('v13@7c3e1a2', or the draft's).</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Depth">How far the neighbors reach and the cascade ran, 0 to 4.</param>
/// <param name="AsOf">When the view was built.</param>
/// <param name="Bands">The three bands.</param>
/// <param name="Edges">Edges across the bands.</param>
/// <param name="Conflicts">Typed disagreements, one per decided node.</param>
/// <param name="Coverage">Counts over the anchor's own scope.</param>
/// <param name="Searched">What was searched: the anchors, the map version, each source's state, which bands are empty (scoping spec C6).</param>
/// <param name="Degraded">Sources that couldn't be read (none in S-4: the view reads stored edges only).</param>
/// <param name="Cites">The citations by marker (empty in S-4: record citations come with the source stages).</param>
public sealed record LensesView(
    int SchemaVersion,
    LensAnchor Anchor,
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
