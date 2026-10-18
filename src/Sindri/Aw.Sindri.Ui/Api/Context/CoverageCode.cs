namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Code coverage.</summary>
/// <param name="Implemented">Implemented, as served.</param>
/// <param name="Of">Of, as served.</param>
/// <param name="Level">Level, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
internal sealed record CoverageCode(
    int Implemented,
    int Of,
    string Level,
    int Suggested);
