namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// What the change rules remember between passes: sessions seen once (not yet reported) and the ones reported, each with its asking episode
/// and the times it was reported asking in the last hour.
/// </summary>
public sealed record FleetTrackerState
{
    /// <summary>Nothing seen, nothing reported.</summary>
    public static FleetTrackerState Empty { get; } = new();

    /// <summary>Sessions seen in the last good pass and not yet reported.</summary>
    public IReadOnlySet<SessionRef> Candidates { get; init; } = new HashSet<SessionRef>();

    /// <summary>Sessions reported as appeared and not yet as gone.</summary>
    public IReadOnlyDictionary<SessionRef, ReportedSession> Reported { get; init; } = new Dictionary<SessionRef, ReportedSession>();
}
