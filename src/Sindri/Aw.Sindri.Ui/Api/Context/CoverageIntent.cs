namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Intent coverage.</summary>
/// <param name="Covered">Covered, as served.</param>
/// <param name="Of">Of, as served.</param>
internal sealed record CoverageIntent(
    int Covered,
    int Of);
