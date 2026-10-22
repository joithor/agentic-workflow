namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One file's symbols in the file map's symbols column.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Symbols">The symbols the file defines, in file order.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="AsOfCommit">The map version's commit, when it differs.</param>
/// <param name="State">'ok' or 'stale' as served.</param>
internal sealed record FileSymbolsEntry(
    string Path,
    IReadOnlyList<SymbolSiteView> Symbols,
    string Commit,
    string? AsOfCommit,
    string State);
