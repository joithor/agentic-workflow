namespace Aw.Context.Core.Extract;

/// <summary>What a cluster source found.</summary>
/// <param name="Clusters">The clusters.</param>
/// <param name="Available">False when the clustering tool is absent or failed: the build goes on with deterministic anchors and reports 'degraded: clusters'.</param>
public sealed record ClusterSet(IReadOnlyList<FileCluster> Clusters, bool Available);
