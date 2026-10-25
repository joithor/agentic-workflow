namespace Aw.Context.Core.Extract;

/// <summary>What a cluster source found.</summary>
/// <param name="Clusters">The clusters.</param>
/// <param name="Available">False when the clustering tool is absent or failed: the build goes on with deterministic anchors and reports 'degraded: clusters'.</param>
/// <param name="Reason">Why the clusters are degraded ('timed out after 60 min', 'graphify not found'): set when unavailable, and when available but incomplete (the snapshot was truncated). Null when nothing was lost.</param>
public sealed record ClusterSet(IReadOnlyList<FileCluster> Clusters, bool Available, string? Reason = null);
