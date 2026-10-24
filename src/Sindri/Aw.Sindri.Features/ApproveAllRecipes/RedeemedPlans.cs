using System.Runtime.CompilerServices;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// The plan an approve-all start redeemed, kept for the job that does the work: the start reads the plan inside the gate (so a refusal is a synchronous
/// error), and the job, which runs afterwards outside it, takes the kept plan. Keyed by the request itself, so nothing but that start's job can take it.
/// </summary>
internal sealed class RedeemedPlans
{
    private readonly ConditionalWeakTable<ApproveAllRecipesRequest, RedeemedPlan> _held = [];

    public void Keep(ApproveAllRecipesRequest request, RedeemedPlan plan) => _held.AddOrUpdate(request, plan);

    public void Drop(ApproveAllRecipesRequest request) => _ = _held.Remove(request);

    public bool Take(ApproveAllRecipesRequest request, out RedeemedPlan? plan)
    {
        if (!_held.TryGetValue(request, out plan))
        {
            return false;
        }

        _ = _held.Remove(request);
        return true;
    }
}
