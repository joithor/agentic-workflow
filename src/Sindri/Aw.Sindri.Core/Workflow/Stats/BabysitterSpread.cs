namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The middle and the high end of a figure across pull requests.</summary>
/// <param name="Median">The median.</param>
/// <param name="P90">The 90th percentile (nearest rank).</param>
public sealed record BabysitterSpread(long? Median, long? P90);
