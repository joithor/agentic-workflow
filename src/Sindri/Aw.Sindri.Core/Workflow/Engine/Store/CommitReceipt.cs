namespace Aw.Sindri.Core.Workflow.Engine.Store;

/// <summary>What a Commit returned (spec §5.3 step 2): the decision under the key, and whether it was already recorded (nothing was written).</summary>
/// <param name="Decision">The decision recorded under the key: this Commit's, or the earlier one's.</param>
/// <param name="AlreadyApplied">True when the key was already recorded; the Commit wrote nothing.</param>
public sealed record CommitReceipt(RecordedDecision Decision, bool AlreadyApplied);
