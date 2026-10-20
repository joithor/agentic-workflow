using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The shipped set until the engine supplies it: this build embeds no recipes, so the plan is empty and nothing is approved. The engine replaces it with its
/// implementation of <see cref="IShippedRecipes"/>.
/// </summary>
public sealed class NoShippedRecipes : IShippedRecipes
{
    /// <inheritdoc/>
    public ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ShippedRecipePlan>>(new ShippedRecipePlan([], []));

    /// <inheritdoc/>
    public ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ApproveAllResult>>(ApproveAllResult.Nothing);
}
