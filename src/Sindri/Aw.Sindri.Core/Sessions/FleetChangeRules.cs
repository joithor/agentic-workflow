namespace Aw.Sindri.Core.Sessions;

/// <summary>Which sessions' changes reach the ledger (collector spec CO3).</summary>
public static class FleetChangeRules
{
    private static readonly TimeSpan _askingAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _hour = TimeSpan.FromHours(1);
    private const int MaxAsksPerHour = 6;

    /// <summary>A session appears after two good passes in a row; goes when a good pass lacks it; and asks once it has asked for 5 s, at most 6 times an hour. A failed pass changes nothing.</summary>
    /// <param name="prior">What the last pass left.</param>
    /// <param name="observed">This pass's sessions.</param>
    /// <param name="now">The time of the pass.</param>
    /// <param name="goodPass">False when the process table failed: no 'gone', nothing forgotten.</param>
    /// <returns>The next state and the changes.</returns>
    public static FleetDiff Diff(FleetTrackerState prior, IReadOnlyList<ObservedSession> observed, DateTimeOffset now, bool goodPass)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(observed);
        if (!goodPass)
        {
            return new FleetDiff(prior, []);
        }

        var current = observed.GroupBy(static s => s.Ref).ToDictionary(static g => g.Key, static g => g.First());
        var reported = new Dictionary<SessionRef, ReportedSession>(prior.Reported);
        var changes = new List<FleetChange>();
        var candidates = new HashSet<SessionRef>();
        Departures(reported, current, changes, now);
        foreach (var session in current.Values.OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal))
        {
            if (!reported.ContainsKey(session.Ref))
            {
                if (prior.Candidates.Contains(session.Ref))
                {
                    reported[session.Ref] = new ReportedSession(session.Pid, session.StartTicks, AskingReported: false, []);
                    changes.Add(Change(session, FleetChangeKind.Appeared, now));
                }
                else
                {
                    _ = candidates.Add(session.Ref);
                }
            }
        }

        foreach (var session in current.Values.Where(s => reported.ContainsKey(s.Ref)).OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal))
        {
            reported[session.Ref] = Asking(reported[session.Ref], session, now, changes);
        }

        return new FleetDiff(new FleetTrackerState { Candidates = candidates, Reported = reported }, changes);
    }

    /// <summary>Rebuilds the reported set from the last 24 hours of 'fleet.changed' rows.</summary>
    /// <param name="rows">The rows, oldest first.</param>
    /// <returns>The state a restarted collector starts from.</returns>
    public static FleetTrackerState Restore(IEnumerable<FleetHistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var reported = new Dictionary<SessionRef, ReportedSession>();
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case FleetChangeKind.Appeared:
                    reported[row.Ref] = new ReportedSession(Pid: null, StartTicks: null, AskingReported: false, []);
                    break;
                case FleetChangeKind.Gone:
                    _ = reported.Remove(row.Ref);
                    break;
                case FleetChangeKind.Asking:
                default:
                    if (reported.TryGetValue(row.Ref, out var known))
                    {
                        reported[row.Ref] = known with { AskingReported = true, Asks = [.. known.Asks, row.At] };   // still asking after a restart is the same episode
                    }

                    break;
            }
        }

        return new FleetTrackerState { Reported = reported };
    }

    private static void Departures(Dictionary<SessionRef, ReportedSession> reported, Dictionary<SessionRef, ObservedSession> current, List<FleetChange> changes, DateTimeOffset now)
    {
        foreach (var (reference, known) in reported.OrderBy(static r => r.Key.ToString(), StringComparer.Ordinal).ToList())
        {
            if (current.ContainsKey(reference))
            {
                continue;
            }

            _ = reported.Remove(reference);
            var renamed = current.Values.FirstOrDefault(s => !reported.ContainsKey(s.Ref) && known.Pid == s.Pid && known.StartTicks == s.StartTicks);
            if (renamed is not null)
            {
                reported[renamed.Ref] = known;   // the same process under a better id (a record appeared): not a departure
            }
            else
            {
                changes.Add(new FleetChange(reference, FleetChangeKind.Gone, SessionState.Unknown, now));
            }
        }
    }

    private static ReportedSession Asking(ReportedSession known, ObservedSession session, DateTimeOffset now, List<FleetChange> changes)
    {
        var withIdentity = known with { Pid = session.Pid, StartTicks = session.StartTicks };
        if (session.State != SessionState.Asking)
        {
            return withIdentity with { AskingReported = false };
        }

        if (withIdentity.AskingReported || now - session.Since < _askingAfter)
        {
            return withIdentity;
        }

        var asks = withIdentity.Asks.Where(at => now - at < _hour).ToList();
        if (asks.Count < MaxAsksPerHour)
        {
            changes.Add(Change(session, FleetChangeKind.Asking, now));
            asks.Add(now);
        }

        return withIdentity with { AskingReported = true, Asks = asks };
    }

    private static FleetChange Change(ObservedSession s, FleetChangeKind kind, DateTimeOffset now) =>
        new(s.Ref, kind, s.State, now) { Reason = s.Reason, Repo = s.Repo, HostKind = s.Host.Kind, LaunchId = s.LaunchId };
}
