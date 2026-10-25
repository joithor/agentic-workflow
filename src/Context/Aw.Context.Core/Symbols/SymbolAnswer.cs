namespace Aw.Context.Core.Symbols;

/// <summary>The rows of one read and the head they were read under.</summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Head">The index head; the rows are empty when it says there is no readable index.</param>
/// <param name="Rows">The rows, in the index's order.</param>
public sealed record SymbolAnswer<T>(CodeIndexHead Head, IReadOnlyList<T> Rows);
