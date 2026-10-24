namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/filemap: one directory of a repo with its status marks.</summary>
/// <param name="SchemaVersion">SchemaVersion, as served.</param>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Sha">Sha, as served.</param>
/// <param name="MapVersion">MapVersion, as served.</param>
/// <param name="Draft">Draft, as served.</param>
/// <param name="Path">Path, as served.</param>
/// <param name="Entries">Entries, as served.</param>
/// <param name="Unmapped">Unmapped, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
/// <param name="Totals">Totals, as served.</param>
/// <param name="Symbols">Symbols, as served.</param>
/// <param name="NextCursor">NextCursor, as served: null on the last page.</param>
/// <param name="Total">Total, as served: the direct children matching the query, over every page.</param>
internal sealed record FileMapView(
    int SchemaVersion,
    string Repo,
    string Sha,
    string MapVersion,
    bool Draft,
    string Path,
    IReadOnlyList<FileMapEntry> Entries,
    IReadOnlyList<UnmappedPath> Unmapped,
    IReadOnlyList<FileSuggestion> Suggested,
    FileMapTotals Totals,
    SymbolsView Symbols,
    string? NextCursor = null,
    int? Total = null);
