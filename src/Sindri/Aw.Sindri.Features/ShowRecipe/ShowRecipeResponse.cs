using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>The recipe's line and its approval preview (engine spec §4.2).</summary>
/// <param name="Recipe">Where it stands, with its inputs.</param>
/// <param name="Preview">What approving it would show.</param>
public sealed record ShowRecipeResponse(RecipeSummary Recipe, ApprovalPreview Preview);
