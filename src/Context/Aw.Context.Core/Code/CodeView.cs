using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Code;

/// <summary>The code lens around some anchors (S-CL, scoping station C3): files, tests, data, callers and callees. </summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Anchors">The anchors that resolved.</param>
/// <param name="MissingAnchors">The names that did not, as given.</param>
/// <param name="MapVersion">The map read ('v13@7c3e1a2').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the view is built.</param>
/// <param name="Files">Files that implement a node of the own scope.</param>
/// <param name="Tests">Test files that cover them.</param>
/// <param name="Data">The data things and their schema files.</param>
/// <param name="Callers">Unavailable until S-9.</param>
/// <param name="Callees">Unavailable until S-9.</param>
/// <param name="Counts">The full counts.</param>
/// <param name="Truncated">Which lists were cut.</param>
/// <param name="Lexical">What the lexical search read.</param>
/// <param name="Edges">The stored implements, tests, writes and reads edges between the found items and the scope.</param>
/// <param name="Coverage">The lenses' coverage over the union of the own scopes, with 'map' and 'byAnchor'.</param>
/// <param name="Searched">What was searched: the map, the code index and the two lexical sources.</param>
/// <param name="Degraded">Lexical sources that couldn't be fully read.</param>
/// <param name="Cites">The citations by marker (none: the code lens cites no records).</param>
public sealed record CodeView(
    int SchemaVersion,
    IReadOnlyList<LensAnchor> Anchors,
    IReadOnlyList<string> MissingAnchors,
    string MapVersion,
    bool Draft,
    DateTimeOffset AsOf,
    IReadOnlyList<CodeFile> Files,
    IReadOnlyList<CodeTest> Tests,
    CodeData Data,
    CodeSymbols Callers,
    CodeSymbols Callees,
    CodeLensCounts Counts,
    CodeLensTruncated Truncated,
    IReadOnlyList<CodeLexical> Lexical,
    IReadOnlyList<LensEdge> Edges,
    LensCoverage Coverage,
    LensSearched Searched,
    IReadOnlyList<string> Degraded,
    IReadOnlyDictionary<string, Citation> Cites);
