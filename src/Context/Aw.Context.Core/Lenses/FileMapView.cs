using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>'context filemap' (spec §8.9): the direct children of a path with rollups.</summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Sha">The commit the tree was listed at (seven characters).</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Path">The directory listed ('' is the root).</param>
/// <param name="Entries">Its direct children (only unmapped and suggested ones when asked).</param>
/// <param name="Unmapped">The unmapped entries, with their file counts.</param>
/// <param name="Suggested">The suggested links under the path, with ids.</param>
/// <param name="Totals">Counts over every file under the path.</param>
/// <param name="Symbols">Always unavailable in S-4.</param>
/// <param name="NextCursor">The cursor of the next page of entries, or null on the last one. Unmapped and Suggested cover this page's entries.</param>
/// <param name="Total">How many direct children match the query (every page).</param>
public sealed record FileMapView(
    int SchemaVersion,
    ContextRepoName Repo,
    string Sha,
    string MapVersion,
    bool Draft,
    string Path,
    IReadOnlyList<FileMapEntry> Entries,
    IReadOnlyList<FileMapUnmapped> Unmapped,
    IReadOnlyList<FileMapSuggestion> Suggested,
    FileMapTotals Totals,
    LensSymbols Symbols,
    string? NextCursor = null,
    int? Total = null);
