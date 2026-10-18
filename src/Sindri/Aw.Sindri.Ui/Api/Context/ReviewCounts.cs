namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Entries per group.</summary>
/// <param name="Deterministic">Deterministic, as served.</param>
/// <param name="Model">Model, as served.</param>
/// <param name="Orphaned">Orphaned, as served.</param>
internal sealed record ReviewCounts(
    int Deterministic,
    int Model,
    int Orphaned);
