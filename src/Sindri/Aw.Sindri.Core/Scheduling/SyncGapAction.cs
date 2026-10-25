namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the 'context-sync-gaps' service does on a tick.</summary>
public enum SyncGapAction
{
    /// <summary>No configured source is waiting for its first sync: a view read and nothing else.</summary>
    Nothing,

    /// <summary>A sync job is running: never doubled.</summary>
    JobRunning,

    /// <summary>The last sync(s) failed and the wait is not over.</summary>
    BackoffNotDue,

    /// <summary>Start one sync of every source.</summary>
    Start,
}
