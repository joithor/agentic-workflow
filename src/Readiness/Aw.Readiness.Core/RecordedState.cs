namespace Aw.Readiness.Core;

/// <summary>One criterion's state as a run recorded it.</summary>
/// <param name="Id">The criterion.</param>
/// <param name="State">Its state on that run.</param>
public sealed record RecordedState(CriterionId Id, CriterionState State);
