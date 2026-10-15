using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>'approved …': the approval row, the preview it was given on, and the drift-parked instances it resumed.</summary>
/// <param name="Accepted">The command id and the ledger sequence of 'recipe.approved'.</param>
/// <param name="Approval">The approval row recorded.</param>
/// <param name="Preview">The preview the challenge was issued with.</param>
/// <param name="Resumed">The instances a drift approval resumed (empty otherwise; woken through IRecipeWake).</param>
public sealed record ApproveRecipeResponse(CommandAccepted Accepted, RecipeApproval Approval, ApprovalPreview Preview, IReadOnlyList<WorkflowInstanceId> Resumed);
