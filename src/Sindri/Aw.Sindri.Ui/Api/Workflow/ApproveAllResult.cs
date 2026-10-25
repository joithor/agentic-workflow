namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the approval did (ApproveAllResult).</summary>
/// <param name="Approved">Approved.</param>
/// <param name="Refused">The checks refused these; the job carried on.</param>
/// <param name="Skipped">Left because something they wait on was refused.</param>
internal sealed record ApproveAllResult(IReadOnlyList<ApprovedRecipe> Approved, IReadOnlyList<RefusedRecipe> Refused, IReadOnlyList<SkippedRecipe> Skipped);
