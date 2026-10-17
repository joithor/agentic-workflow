namespace Aw.Sindri.Core.Sessions;

/// <summary>The collector's latest pass (collector spec §2): every session, the recently ended ones, one problem per failed source, and when it was taken.</summary>
/// <param name="Sessions">The live sessions.</param>
/// <param name="RecentlyEnded">The sessions that ended in the last 10 minutes.</param>
/// <param name="Problems">One row per source that failed.</param>
/// <param name="ObservedAt">When the pass ran.</param>
public sealed record SessionSnapshot(
    IReadOnlyList<ObservedSession> Sessions, IReadOnlyList<EndedSession> RecentlyEnded, IReadOnlyList<SessionProblem> Problems, DateTimeOffset ObservedAt);
