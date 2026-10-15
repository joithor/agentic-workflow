using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>'context lenses &lt;anchor&gt;' (spec §8.8): intent, product and code bands around one node, with the edges across them, their conflicts and coverage.</summary>
/// <param name="Map">The map read ('v13@7c3e1a2', or the draft's).</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Anchor">The anchor node.</param>
/// <param name="Intent">Decisions and records.</param>
/// <param name="Product">The anchor's own scope, neighbors and what it affects, deduplicated in that order.</param>
/// <param name="Code">Files with 'implements' or 'tests' edges.</param>
/// <param name="Symbols">Always unavailable in S-4.</param>
/// <param name="Edges">Edges across the bands.</param>
/// <param name="Conflicts">Typed disagreements.</param>
/// <param name="Coverage">Counts over the anchor's scope.</param>
/// <param name="Gaps">Scope nodes with no intent or no code, as one line each.</param>
public sealed record LensesView(
    string Map,
    bool Draft,
    NodeKey Anchor,
    LensBand Intent,
    LensBand Product,
    LensBand Code,
    LensSymbols Symbols,
    IReadOnlyList<LensEdge> Edges,
    IReadOnlyList<LensConflict> Conflicts,
    LensCoverage Coverage,
    IReadOnlyList<string> Gaps);
