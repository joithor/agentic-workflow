namespace Aw.Context.Core.Symbols;

/// <summary>The callers or callees of one symbol.</summary>
/// <param name="Head">The index head.</param>
/// <param name="Rows">The edges, at most 100 per level.</param>
/// <param name="Unresolved">How many call sites the index could not tie to a symbol.</param>
public sealed record CallAnswer(CodeIndexHead Head, IReadOnlyList<CallEdgeRow> Rows, int Unresolved);
