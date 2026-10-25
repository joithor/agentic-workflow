namespace Aw.CodeIndex.Core.Model;

/// <summary>What an index holds.</summary>
/// <param name="Files">Indexed files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Resolved edges.</param>
public sealed record IndexCounts(int Files, int Symbols, int Edges);
