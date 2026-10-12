using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>The home's recipes, read as they are, whose approval check answers 'revoked' (engine spec §5.2, W11): a recipe revoked while its instance runs.</summary>
/// <param name="inner">The home's recipes.</param>
internal sealed class RevokedRecipes(IApprovedRecipes inner) : IApprovedRecipes
{
    public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => inner.Latest(id, cancellationToken);

    public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => inner.Find(id, hash, cancellationToken);

    public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ApprovalCheck>>(ApprovalCheck.Revoked);
}
