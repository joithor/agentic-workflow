namespace Aw.Sindri.Core.Sessions;

/// <summary>Which sessions' changes reach the ledger (collector spec CO3).</summary>
public static class FleetChangeRules
{
    /// <summary>A session appears after two good passes in a row; goes when a good pass lacks it; and asks once it has asked for 5 s, at most 6 times an hour. A failed pass changes nothing.</summary>
    /// <param name="prior">What the last pass left.</param>
    /// <param name="observed">This pass's sessions.</param>
    /// <param name="now">The time of the pass.</param>
    /// <param name="goodPass">False when the process table failed: no 'gone', nothing forgotten.</param>
    /// <returns>The next state and the changes.</returns>
    public static FleetDiff Diff(FleetTrackerState prior, IReadOnlyList<ObservedSession> observed, DateTimeOffset now, bool goodPass) => throw new NotSupportedException();

    /// <summary>Rebuilds the reported set from the last 24 hours of 'fleet.changed' rows.</summary>
    /// <param name="rows">The rows, oldest first.</param>
    /// <returns>The state a restarted collector starts from.</returns>
    public static FleetTrackerState Restore(IEnumerable<FleetHistoryRow> rows) => throw new NotSupportedException();
}
