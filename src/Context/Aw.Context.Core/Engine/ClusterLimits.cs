namespace Aw.Context.Core.Engine;

/// <summary>
/// 'context.extract.graphify': what the cluster pass may use (spec S-4). Whole numbers; 0 is no limit. The defaults are
/// generous so a large repo clusters completely; a profile lowers them to bound a slow machine.
/// </summary>
public sealed record ClusterLimits
{
    private const long Mb = 1024L * 1024;

    /// <summary>The shipped limits.</summary>
    public static ClusterLimits Default { get; } = new();

    /// <summary>The most minutes one graphify run may take, 0 to 70000 (about 48 days, what a timer accepts); 0 for no limit. Default 60.</summary>
    public int TimeoutMinutes { get; init; } = 60;

    /// <summary>The most megabytes of files copied into the snapshot graphify reads; 0 for no limit. Default 2048.</summary>
    public int MaxSnapshotMB { get; init; } = 2048;

    /// <summary>The largest single file, in megabytes, copied into the snapshot; 0 for no limit. Default 16.</summary>
    public int MaxFileMB { get; init; } = 16;

    /// <summary>The most megabytes of graph.json read back; 0 for no limit, which holds the whole parsed graph in memory. Default 256.</summary>
    public int MaxGraphMB { get; init; } = 256;

    /// <summary>The snapshot's byte cap, or null for none.</summary>
    public long? MaxSnapshotBytes => MaxSnapshotMB == 0 ? null : MaxSnapshotMB * Mb;

    /// <summary>The per-file byte cap, or null for none.</summary>
    public long? MaxFileBytes => MaxFileMB == 0 ? null : MaxFileMB * Mb;

    /// <summary>The graph.json byte cap, or null for none.</summary>
    public long? MaxGraphBytes => MaxGraphMB == 0 ? null : MaxGraphMB * Mb;
}
