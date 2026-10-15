namespace Aw.Meta.Core.Watch;

/// <summary>Turns successive polls into one line per settled change.</summary>
public static class WatchDiff
{
    /// <summary>Compares a poll with what the watch remembers.</summary>
    /// <param name="state">The state after the previous poll.</param>
    /// <param name="snapshot">This poll.</param>
    /// <returns>The lines to report and the new state. The first poll only sets the baseline.</returns>
    public static WatchStep Step(WatchState state, WatchSnapshot snapshot) => throw new NotSupportedException("WatchDiff: make WatchDiffTests pass");
}
