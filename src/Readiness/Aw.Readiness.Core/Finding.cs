namespace Aw.Readiness.Core;

/// <summary>A criterion's outcome on one run, with the evidence behind it.</summary>
/// <param name="Criterion">The criterion.</param>
/// <param name="State">Its state.</param>
/// <param name="Evidence">What was measured: the files, values or remote answers the state rests on.</param>
/// <param name="Unread">True when the state is unknown only because its data couldn't be read this run (offline, gh
/// missing or failing). Such a criterion can't confirm a target it gates, and isn't compared between runs.</param>
public sealed record Finding(Criterion Criterion, CriterionState State, string Evidence, bool Unread = false);
