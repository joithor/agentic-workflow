namespace Aw.Readiness.Core;

/// <summary>A criterion whose state changed between two runs. A null side means it wasn't in that run.</summary>
/// <param name="Id">The criterion.</param>
/// <param name="Before">Its state on the previous run.</param>
/// <param name="After">Its state on this run.</param>
public sealed record StateChange(CriterionId Id, CriterionState? Before, CriterionState? After);
