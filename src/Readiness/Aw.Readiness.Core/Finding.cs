namespace Aw.Readiness.Core;

/// <summary>A criterion's outcome on one run, with the evidence behind it.</summary>
/// <param name="Criterion">The criterion.</param>
/// <param name="State">Its state.</param>
/// <param name="Evidence">What was measured: the files, values or remote answers the state rests on.</param>
public sealed record Finding(Criterion Criterion, CriterionState State, string Evidence);
