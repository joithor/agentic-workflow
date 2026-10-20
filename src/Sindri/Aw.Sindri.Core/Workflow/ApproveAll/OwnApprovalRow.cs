using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>A previously approved recipe that is approved on its own, typed, with ApproveRecipe (setup-once spec §5.3, §7.2).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="DisplayName">The name shown instead of the id.</param>
/// <param name="Reason">Why it is not in the hold.</param>
/// <param name="Sentence">The plain sentence the owner reads.</param>
/// <param name="Added">The capabilities it gained; empty when it only waits for a recipe it uses.</param>
public sealed record OwnApprovalRow(RecipeId Id, string DisplayName, OwnApprovalReason Reason, string Sentence, IReadOnlyList<CapabilityLine> Added);
