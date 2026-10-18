using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetLaunchTargets;

namespace Aw.Sindri.Features.GetItem;

/// <summary>Reads the projections an item's launch block needs and hands them to <see cref="LaunchOptions.For"/>, which decides.</summary>
/// <param name="launches">The item's and the repo's launches.</param>
/// <param name="targets">The last probe with budgets.</param>
/// <param name="approvals">The approved profile: launch settings and repo paths.</param>
/// <param name="catalog">The agent catalog.</param>
/// <param name="clock">Now.</param>
internal sealed class ItemLaunchSource(ILaunchViews launches, LaunchTargetsReader targets, IApprovedProfile approvals, IAgentCatalog catalog, TimeProvider clock) : IItemLaunchSource
{
    public async ValueTask<Result<ItemLaunch>> For(ItemRow item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var own = await launches.ForItem(item.Id, cancellationToken).ConfigureAwait(false);
        if (own.IsError)
        {
            return own.Error;
        }

        var inRepo = item.Repo is { } repo ? await launches.ForRepo(repo, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<LaunchRow>>([]);
        if (inRepo.IsError)
        {
            return inRepo.Error;
        }

        var probe = await targets.Read(cancellationToken).ConfigureAwait(false);
        if (probe.IsError)
        {
            return probe.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var profile = approved.Value?.Profile;
        return LaunchOptions.For(new LaunchOptionInputs
        {
            Item = item,
            Launches = own.Value,
            Probe = probe.Value,
            Settings = profile?.Launch,
            LastUsedInRepo = inRepo.Value.FirstOrDefault(static row => row.State == LaunchState.Started)?.Target,
            RepoPath = item.Repo is { } named ? profile?.Repos?.FirstOrDefault(r => string.Equals(r.Name, named.Value, StringComparison.Ordinal))?.Path : null,
            InCatalog = id => catalog.Resolve(id) is not null,
            Now = clock.GetUtcNow(),
        });
    }
}
