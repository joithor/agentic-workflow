using Aw.Kernel;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>Takes one snapshot: GitHub's pull requests, origin's main, the state disk and the inbox.</summary>
/// <param name="repo">The gh and git calls.</param>
/// <param name="resources">The disk.</param>
/// <param name="messages">The inbox.</param>
internal sealed class WatchPoller(RepoCommands repo, IHostResources resources, IMessageStore messages)
{
    /// <summary>Disk is low below this many free bytes.</summary>
    internal const long DiskLowBytes = 5L * 1024 * 1024 * 1024;

    internal async ValueTask<Result<Polled>> Poll(string repoDirectory, CancellationToken cancellationToken)
    {
        var prs = await repo.List(repoDirectory).ConfigureAwait(false);
        var main = prs.IsError ? Results.Failure<string>(prs.Error) : await repo.RemoteMain(repoDirectory).ConfigureAwait(false);
        var disk = await resources.Read(cancellationToken).ConfigureAwait(false);
        var inbox = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (prs.IsError || main.IsError || disk.IsError || inbox.IsError)
        {
            return prs.IsError ? prs.Error : main.IsError ? main.Error : disk.IsError ? disk.Error : inbox.Error;
        }

        return new Polled(new WatchSnapshot(prs.Value, main.Value, disk.Value.DiskFreeBytes < DiskLowBytes), inbox.Value.Count);
    }
}
