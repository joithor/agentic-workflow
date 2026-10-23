using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The pull requests the live instances wait on (PR babysitter spec §6.2): what the PR watcher polls.</summary>
internal interface IWatchedPulls
{
    /// <summary>Lists the pull requests of the live instances that wait on 'pr.changed'.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>One row per instance, or the error that stopped the listing.</returns>
    public ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken);
}
