using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features;

/// <summary>
/// The shipped recipes' plan, once a profile is approved (setup-once spec §5.3, §7.6): every approval check uses the approved profile's allowlist, so before one
/// there is no plan, no challenge and no approval (SND-SETUP-007). Shared by the plan query, the challenge issue and the approve-all start.
/// </summary>
/// <param name="profile">The approved profile.</param>
/// <param name="shipped">The recipes this build ships.</param>
internal sealed class ApproveAllPlanReader(IApprovedProfile profile, IShippedRecipes shipped)
{
    public async ValueTask<Result<ShippedRecipePlan>> Read(CancellationToken cancellationToken)
    {
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        return approved.Value is null
            ? SindriErrors.SetupProfileNotApproved.With("approve-all waits for an approved profile (fix: approve the profile first)")
            : await shipped.Plan(cancellationToken).ConfigureAwait(false);
    }
}
