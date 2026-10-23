namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Docs pull requests merged with their source or left.</summary>
/// <param name="WithMerge">Entries into the docs-merging state: the docs PR merged with the source.</param>
/// <param name="OwnerDesk">Entries into the docs-merge desk: the source merged elsewhere and the owner decides.</param>
/// <param name="ByOutcome">The merge record's 'docsMerge' rows by outcome (merged, pending, none, doneByHand, left).</param>
public sealed record BabysitterDocsStats(int WithMerge, int OwnerDesk, IReadOnlyList<ReasonCount> ByOutcome);
