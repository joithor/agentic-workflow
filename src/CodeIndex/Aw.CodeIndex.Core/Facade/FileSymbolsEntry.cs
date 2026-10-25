namespace Aw.CodeIndex.Core.Facade;

/// <summary>The symbols one file defines.</summary>
/// <param name="Path">The file's repo-relative path.</param>
/// <param name="Symbols">Its symbols in line order; empty for a file the index does not know.</param>
public sealed record FileSymbolsEntry(string Path, IReadOnlyList<SymbolHit> Symbols);
