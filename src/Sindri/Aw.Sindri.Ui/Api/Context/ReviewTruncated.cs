namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Whether a group was cut.</summary>
/// <param name="Deterministic">Deterministic, as served.</param>
/// <param name="Model">Model, as served.</param>
/// <param name="Orphaned">Orphaned, as served.</param>
internal sealed record ReviewTruncated(
    bool Deterministic,
    bool Model,
    bool Orphaned);
