namespace Aw.Context.Core.Extract;

/// <summary>What a labeling pass named.</summary>
/// <param name="Labels">A label per cluster the model could name.</param>
/// <param name="Complete">False when a model call failed after some batches had been answered: <paramref name="Labels"/> holds what those named, and the pass is degraded.</param>
public sealed record LabelPass(IReadOnlyList<ClusterLabel> Labels, bool Complete = true);
