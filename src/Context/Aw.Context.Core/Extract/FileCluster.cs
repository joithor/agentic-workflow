namespace Aw.Context.Core.Extract;

/// <summary>A group of files that belong together, found by clustering the code graph: a candidate feature.</summary>
/// <param name="Id">A stable name for the cluster ('c3').</param>
/// <param name="Files">Its files (paths from the repo root).</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record FileCluster(ClusterId Id, IReadOnlyList<string> Files, double Confidence);
