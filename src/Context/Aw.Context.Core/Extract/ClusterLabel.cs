namespace Aw.Context.Core.Extract;

/// <summary>A model's name for a cluster.</summary>
/// <param name="Id">The cluster's id.</param>
/// <param name="Label">The feature's name ('Tab groups').</param>
public sealed record ClusterLabel(ClusterId Id, string Label);
