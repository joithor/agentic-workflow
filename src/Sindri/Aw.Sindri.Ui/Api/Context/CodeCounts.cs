namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The code band's counts.</summary>
/// <param name="Files">Files, as served.</param>
/// <param name="Symbols">Symbols, as served.</param>
/// <param name="FeaturesWithNoCode">FeaturesWithNoCode, as served.</param>
internal sealed record CodeCounts(
    int Files,
    int Symbols,
    int FeaturesWithNoCode);
