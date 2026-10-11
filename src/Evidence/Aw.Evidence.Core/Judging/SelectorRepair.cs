using Aw.Evidence.Core.Usage;

namespace Aw.Evidence.Core.Judging;

/// <summary>A proposed replacement. Nothing counts as repaired until the action runs on it and the step's expectation passes.</summary>
/// <param name="Decision">Whether a candidate was chosen.</param>
/// <param name="ChosenIndex">The chosen candidate's index.</param>
/// <param name="Usage">What the model reported about the call.</param>
public sealed record SelectorRepair(RepairDecision Decision, int? ChosenIndex, InvocationUsage? Usage);
