namespace Aw.Context.Core.Symbols;

/// <summary>A symbol search: an exact name, 'path', 'path#name' or ref, or a name prefix. The index never treats the text as a wildcard.</summary>
/// <param name="Text">What to find.</param>
/// <param name="Prefix">True to match names that start with <paramref name="Text"/>.</param>
/// <param name="Max">The most hits, at most 200.</param>
public sealed record SymbolQuery(string Text, bool Prefix, int Max);
