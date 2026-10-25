using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// The work of the approve-all job (setup-once spec §5.3): takes the plan the start redeemed and hands it to the engine's shipped-recipe approval, which approves
/// each recipe, skips a refused one and the recipes that wait on it, and carries on. One progress line per recipe; the result is on the job.
/// </summary>
/// <param name="redeemed">The plans starts redeemed.</param>
/// <param name="shipped">The recipes this build ships.</param>
/// <param name="jobs">The job running this work, where its progress lines go.</param>
internal sealed class ApproveAllRecipesService(RedeemedPlans redeemed, IShippedRecipes shipped, IJobContextAccessor jobs) : IService<ApproveAllRecipesRequest, ApproveAllResult>
{
    public async ValueTask<Result<ApproveAllResult>> Handle(ApproveAllRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!redeemed.Take(request, out var plan) || plan is null)
        {
            return SindriErrors.ApiInternal.With("the approve-all job has no redeemed plan");
        }

        var progress = jobs.Current?.Progress ?? new Progress<string>();
        return await shipped.Approve(plan, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The job's one-line summary: '3 approved, 1 refused, 2 skipped'.</summary>
    /// <param name="result">What the job returned.</param>
    /// <returns>The line.</returns>
    internal static string Summary(ApproveAllResult result) =>
        string.Create(CultureInfo.InvariantCulture, $"{result.Approved.Count} approved, {result.Refused.Count} refused, {result.Skipped.Count} skipped");
}
