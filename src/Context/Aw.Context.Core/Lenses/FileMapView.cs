using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>'context filemap' (spec §8.9): the direct children of a path with rollups.</summary>
/// <param name="Map">The map read.</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Path">The directory listed ('' is the root).</param>
/// <param name="Entries">Its direct children (only unmapped and suggested ones when asked).</param>
/// <param name="Suggested">The suggested links under the path, with ids.</param>
/// <param name="Totals">Counts over every file under the path.</param>
/// <param name="Symbols">Always unavailable in S-4.</param>
public sealed record FileMapView(
    string Map,
    bool Draft,
    ContextRepoName Repo,
    string Path,
    IReadOnlyList<FileMapEntry> Entries,
    IReadOnlyList<FileMapSuggestion> Suggested,
    FileMapTotals Totals,
    LensSymbols Symbols);
