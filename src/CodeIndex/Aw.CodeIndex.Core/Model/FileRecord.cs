namespace Aw.CodeIndex.Core.Model;

/// <summary>One file and everything derived from it, written to the build file in one go (spec §4.2 'files').</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The git object id it was read from.</param>
/// <param name="Size">The size in bytes.</param>
/// <param name="Language">The language word from the extension table ('csharp', 'typescript', ...).</param>
/// <param name="Parsed">False when only graphify or deps read it, or the parser skipped it.</param>
/// <param name="Symbols">Its symbols.</param>
public sealed record FileRecord(string Path, string Blob, long Size, string Language, bool Parsed, IReadOnlyList<SymbolRecord> Symbols);
