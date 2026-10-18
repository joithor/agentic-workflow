namespace Aw.Sindri.Core.Sessions;

/// <summary>The state history the snapshot keeps (collector spec CA-2): the last 20 state changes of each live session in this Sindri run, in memory. Pure.</summary>
public static class StateHistoryRules
{
    /// <summary>The most entries kept per session.</summary>
    public const int Cap = 20;

    /// <summary>The history after a pass: a session's first state is an entry, so is each change of state or asking reason; a repeat is not; a session that left loses its history.</summary>
    /// <param name="previous">The history before the pass.</param>
    /// <param name="sessions">The sessions of the pass.</param>
    /// <returns>The new history.</returns>
    public static IReadOnlyDictionary<SessionRef, IReadOnlyList<SessionStateEntry>> Next(
        IReadOnlyDictionary<SessionRef, IReadOnlyList<SessionStateEntry>> previous, IReadOnlyList<ObservedSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(sessions);
        var next = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>>();
        foreach (var session in sessions)
        {
            var entries = previous.GetValueOrDefault(session.Ref) ?? [];
            var last = entries.Count == 0 ? null : entries[^1];
            next[session.Ref] = last is not null && last.State == session.State && last.Reason == session.Reason
                ? entries
                : [.. entries.Append(new SessionStateEntry(session.State, session.Reason, session.Since, session.Source)).TakeLast(Cap)];
        }

        return next;
    }
}
