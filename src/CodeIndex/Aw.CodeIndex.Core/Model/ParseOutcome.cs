namespace Aw.CodeIndex.Core.Model;

/// <summary>One file's parse: its symbols, or the reason it was left unparsed (spec §5.3, CI12: depth and time guards).</summary>
/// <param name="Symbols">The symbols; empty when <paramref name="Skip"/> is set.</param>
/// <param name="Skip">Why the file ended 'parsed = 0' ('too-deep', 'too-many-symbols', 'too-many-call-sites', 'timeout'), or null.</param>
public sealed record ParseOutcome(IReadOnlyList<ParsedSymbol> Symbols, string? Skip = null);
