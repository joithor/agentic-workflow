namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>What an index holds.</summary>
/// <param name="Files">Files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Edges.</param>
internal sealed record CodeIndexCountsView(int Files, int Symbols, int Edges);
