namespace Aw.Context.Core.Engine;

/// <summary>
/// 'context.extract.graphify': what the cluster pass may use (spec S-4). Whole numbers; 0 is no limit. The defaults are
/// generous so a large repo clusters completely; a profile lowers them to bound a slow machine.
/// </summary>
public sealed record GraphifyLimits
{
    private const long Mb = 1024L * 1024;

    /// <summary>The shipped limits.</summary>
    public static GraphifyLimits Default { get; } = new();

    /// <summary>The most minutes one graphify run may take; 0 for no limit. Default 60.</summary>
    public int TimeoutMinutes { get; init; } = 60;

    /// <summary>The most megabytes of files copied into the snapshot graphify reads; 0 for no limit. Default 2048.</summary>
    public int MaxSnapshotMB { get; init; } = 2048;

    /// <summary>The largest single file, in megabytes, copied into the snapshot; 0 for no limit. Default 16.</summary>
    public int MaxFileMB { get; init; } = 16;

    /// <summary>The most megabytes of graph.json read back; 0 for no limit. Default 1024.</summary>
    public int MaxGraphMB { get; init; } = 1024;

    /// <summary>A time limit that replaces <see cref="TimeoutMinutes"/>, so a test needn't wait a whole minute. Never set from a profile.</summary>
    internal TimeSpan? TimeoutOverride { get; init; }

    /// <summary>The run's time limit, or null for none.</summary>
    public TimeSpan? Timeout => TimeoutOverride ?? (TimeoutMinutes == 0 ? null : TimeSpan.FromMinutes(TimeoutMinutes));

    /// <summary>The snapshot's byte cap, or null for none.</summary>
    public long? MaxSnapshotBytes => MaxSnapshotMB == 0 ? null : MaxSnapshotMB * Mb;

    /// <summary>The per-file byte cap, or null for none.</summary>
    public long? MaxFileBytes => MaxFileMB == 0 ? null : MaxFileMB * Mb;

    /// <summary>The graph.json byte cap, or null for none.</summary>
    public long? MaxGraphBytes => MaxGraphMB == 0 ? null : MaxGraphMB * Mb;
}
