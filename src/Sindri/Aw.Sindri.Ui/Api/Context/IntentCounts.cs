namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The intent band's counts; Orphaned is null while the API does not serve it.</summary>
/// <param name="Records">Records, as served.</param>
/// <param name="Decisions">Decisions, as served.</param>
/// <param name="Orphaned">Orphaned, as served.</param>
internal sealed record IntentCounts(
    int Records,
    int Decisions,
    int? Orphaned);
