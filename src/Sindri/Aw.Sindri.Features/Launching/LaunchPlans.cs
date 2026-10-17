using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Launching;

/// <summary>Turns a request into the launcher's input (UI spec E-1): the attended launch of the request's agent in the item's repo checkout.</summary>
/// <param name="reader">Reads the item.</param>
/// <param name="profile">The approved profile, which names each repo's path and each agent's daily budget.</param>
/// <param name="spend">What each agent has spent today, for the budget re-check (screens spec E-28).</param>
internal sealed class LaunchPlans(IViewReader reader, IApprovedProfile profile, ILaunchSpend spend)
{
    /// <summary>Builds the launch.</summary>
    /// <param name="pending">The request.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The launch, or SND-LAUNCH-004 when the item's repo has no path in the approved profile.</returns>
    public async ValueTask<Result<LaunchRequest>> Build(PendingLaunch pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        _ = spend;   // red: the budget is not re-checked yet
        var request = pending.Request;
        var item = await reader.Get<ItemRow>(ViewNames.Items, request.Item.Value, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var path = item.Value?.Repo is { } repo ? approved.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal))?.Path : null;
        return path is null
            ? SindriErrors.LaunchRepoMissing.With($"{request.Item.Value} has no repo with a path in the approved profile (fix: add the repo to repos and approve)")
            : new LaunchRequest(request.Launch, request.Item, request.Target, request.Mode, Attended: true, path, Instance: null, Worktree: null, Step: null, Run: null);
    }
}
