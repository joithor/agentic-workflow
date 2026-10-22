namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>GET /sessions: the live agent sessions the collector sees, the recently ended ones, its problems and the head counts.</summary>
/// <param name="Sessions">Sessions, as served.</param>
/// <param name="RecentlyEnded">RecentlyEnded, as served.</param>
/// <param name="Problems">Problems, as served.</param>
/// <param name="Counts">Counts, as served (taken before any filter).</param>
/// <param name="Empty">Empty, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
internal sealed record SessionList(
    IReadOnlyList<SessionRowView> Sessions,
    IReadOnlyList<EndedSessionView> RecentlyEnded,
    IReadOnlyList<SessionProblemView> Problems,
    SessionListCounts Counts,
    string? Empty,
    DateTimeOffset AsOf);
