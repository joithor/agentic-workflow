namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to FileSymbols.</summary>
/// <param name="Stamp">Which index answered.</param>
/// <param name="Files">One entry per asked path (at most <see cref="CodeIndexRules.MaxFileSymbolPaths"/>).</param>
public sealed record FileSymbolsAnswer(AnswerStamp Stamp, IReadOnlyList<FileSymbolsEntry> Files);
