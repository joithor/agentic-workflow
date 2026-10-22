namespace Aw.Context.Core.Symbols;

/// <summary>One symbol of the index: where it is and what it looks like. Names, signatures and paths are repo text, so the caller treats them as untrusted.</summary>
/// <param name="Ref">The symbol's reference ('path#name').</param>
/// <param name="Name">Its name.</param>
/// <param name="Kind">Its kind ('method', 'class', ...).</param>
/// <param name="Path">The file that defines it.</param>
/// <param name="StartLine">The first line of its span.</param>
/// <param name="EndLine">The last line of its span.</param>
/// <param name="Signature">Its signature, or null.</param>
/// <param name="Exported">True when it is visible outside its file.</param>
public sealed record SymbolSite(string Ref, string Name, string Kind, string Path, int StartLine, int EndLine, string? Signature, bool Exported);
