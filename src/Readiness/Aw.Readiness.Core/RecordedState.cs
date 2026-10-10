namespace Aw.Readiness.Core;

/// <summary>One criterion's state as a run recorded it.</summary>
/// <param name="Id">The criterion.</param>
/// <param name="State">Its state on that run.</param>
/// <param name="Unread">True when its data wasn't read on that run (it isn't compared with other runs).</param>
public sealed record RecordedState(CriterionId Id, CriterionState State, bool Unread = false);
