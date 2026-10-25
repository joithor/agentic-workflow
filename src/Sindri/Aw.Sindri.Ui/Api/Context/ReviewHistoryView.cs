namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/map/review/history.</summary>
/// <param name="Entries">Entries, as served.</param>
/// <param name="Truncated">Truncated, as served.</param>
internal sealed record ReviewHistoryView(
    IReadOnlyList<ReviewHistoryEntry> Entries,
    bool Truncated);
