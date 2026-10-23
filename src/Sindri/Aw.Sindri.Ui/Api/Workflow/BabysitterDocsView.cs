namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>How the documentation updates went.</summary>
/// <param name="WithMerge">Pull requests whose docs merged with them.</param>
/// <param name="OwnerDesk">Docs that waited for you.</param>
/// <param name="ByOutcome">Counts by outcome, as served (merged, pending, none, doneByHand, left).</param>
internal sealed record BabysitterDocsView(int WithMerge, int OwnerDesk, IReadOnlyList<BabysitterDocOutcomeView> ByOutcome);
