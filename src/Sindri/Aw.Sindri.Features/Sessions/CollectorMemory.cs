using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// What the collector keeps between passes (a singleton; the host's loop is its only writer): the change rules' state, the last good pass's sessions and
/// the recently ended ones. Live state is memory only (collector spec CO3); the ledger gets the changes.
/// </summary>
internal sealed class CollectorMemory
{
    internal static readonly TimeSpan EndedFor = TimeSpan.FromMinutes(10);

    internal FleetTrackerState Tracker { get; set; } = FleetTrackerState.Empty;

    internal bool Restored { get; set; }

    internal IReadOnlyDictionary<SessionRef, ObservedSession> Previous { get; set; } = new Dictionary<SessionRef, ObservedSession>();

    internal IReadOnlyList<EndedSession> Ended { get; set; } = [];

    /// <summary>The sessions that left this good pass join the ended list; the ones that returned leave it; the ones older than ten minutes are dropped.</summary>
    internal IReadOnlyList<EndedSession> Ending(IReadOnlyList<ObservedSession> now, DateTimeOffset at)
    {
        var live = now.Select(static s => s.Ref).ToHashSet();
        var gone = Previous.Values.Where(s => !live.Contains(s.Ref)).Select(s => new EndedSession(s, at));
        return [.. Ended.Where(e => !live.Contains(e.Session.Ref)).Concat(gone).Where(e => at - e.EndedAt <= EndedFor).OrderByDescending(static e => e.EndedAt)];
    }
}
