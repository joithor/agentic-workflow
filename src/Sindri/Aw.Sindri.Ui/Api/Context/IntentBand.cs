namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The intent lens.</summary>
/// <param name="Records">Records, as served.</param>
/// <param name="Decisions">Decisions, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="Truncated">Truncated, as served.</param>
internal sealed record IntentBand(
    IReadOnlyList<LensRecord> Records,
    IReadOnlyList<LensDecision> Decisions,
    IntentCounts Counts,
    bool Truncated);
