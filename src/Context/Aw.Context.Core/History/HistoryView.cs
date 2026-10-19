using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.History;

/// <summary>
/// The history lens (scoping spec C5, C10): the stored records and decisions the anchors' own scopes reach, the landed changes of the files that implement them, and
/// what was searched. A 'before' cuts all three to the world as of that instant (records created after it dropped, edited after it 'leaky').
/// </summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Anchors">The anchors that resolved, in request order.</param>
/// <param name="MissingAnchors">The values that named no node this caller may read; one answer for every reason.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the view was built.</param>
/// <param name="Window">The window asked for.</param>
/// <param name="Records">The stored records, newest update first, at most 100.</param>
/// <param name="Decisions">The decisions, newest first, at most 100.</param>
/// <param name="Changes">The landed changes, newest first, at most 100.</param>
/// <param name="Incidents">The incident source's answer.</param>
/// <param name="Edges">Mentions, decides, quotes and supersedes among the records, decisions and the own scope.</param>
/// <param name="Counts">What was found before the caps.</param>
/// <param name="Truncated">Which lists were cut.</param>
/// <param name="Git">What the git step read per repo.</param>
/// <param name="Coverage">The lenses' coverage over the union of the anchors' own scopes.</param>
/// <param name="Searched">What was searched.</param>
/// <param name="Degraded">Sources that couldn't be read in full, one line each.</param>
/// <param name="Cites">The citations by marker (none yet).</param>
public sealed record HistoryView(
    int SchemaVersion,
    IReadOnlyList<LensAnchor> Anchors,
    IReadOnlyList<string> MissingAnchors,
    string MapVersion,
    bool Draft,
    DateTimeOffset AsOf,
    HistoryWindow Window,
    IReadOnlyList<HistoryRecord> Records,
    IReadOnlyList<HistoryDecision> Decisions,
    IReadOnlyList<HistoryChange> Changes,
    HistoryIncidents Incidents,
    IReadOnlyList<LensEdge> Edges,
    HistoryCounts Counts,
    HistoryTruncated Truncated,
    IReadOnlyList<HistoryGit> Git,
    LensCoverage Coverage,
    LensSearched Searched,
    IReadOnlyList<string> Degraded,
    IReadOnlyDictionary<string, Citation> Cites);
