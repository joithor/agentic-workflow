namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The code lens.</summary>
/// <param name="Files">Files, as served.</param>
/// <param name="Symbols">Symbols, as served.</param>
/// <param name="Counts">Counts, as served.</param>
internal sealed record CodeBand(
    IReadOnlyList<CodeFile> Files,
    SymbolsView Symbols,
    CodeCounts Counts);
