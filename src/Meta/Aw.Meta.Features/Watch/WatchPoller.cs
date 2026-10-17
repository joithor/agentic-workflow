using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>Takes one snapshot: GitHub's pull requests, origin's main, the state disk and memory against the team's floors, and the inbox.</summary>
/// <param name="repo">The gh and git calls.</param>
/// <param name="resources">The disk and memory.</param>
/// <param name="messages">The inbox.</param>
/// <param name="values">Keeps the pull requests seen merged or closed, which the merge queue drops.</param>
/// <param name="sweeper">Sweeps the worktrees of the pull requests seen to end.</param>
internal sealed class WatchPoller(RepoCommands repo, IHostResources resources, IMessageStore messages, IKeyValueStore values, WatchSweeper sweeper)
{
    internal async ValueTask<Result<Polled>> Poll(TeamConfig team, CancellationToken cancellationToken)
    {
        var repoDirectory = team.Repo;
        var prs = await repo.List(repoDirectory).ConfigureAwait(false);
        var main = prs.IsError ? Results.Failure<string>(prs.Error) : await repo.RemoteMain(repoDirectory).ConfigureAwait(false);
        var disk = await resources.Read(cancellationToken).ConfigureAwait(false);
        var inbox = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (prs.IsError || main.IsError || disk.IsError || inbox.IsError)
        {
            return prs.IsError ? prs.Error : main.IsError ? main.Error : disk.IsError ? disk.Error : inbox.Error;
        }

        var remembered = await Remember(prs.Value, cancellationToken).ConfigureAwait(false);
        if (remembered.IsError)
        {
            return remembered.Error;
        }

        var worktrees = await sweeper.Sweep(remembered.Value, cancellationToken).ConfigureAwait(false);
        return new Polled(new WatchSnapshot(prs.Value, main.Value, ResourceRules.DiskLow(disk.Value, team.Resources), ResourceRules.MemoryLow(disk.Value, team.Resources)), inbox.Value.Count, worktrees);
    }

    // Keeps the pull requests GitHub shows merged or closed, so the merge queue can drop them; returns the ones not seen ended before.
    private async ValueTask<Result<IReadOnlyList<int>>> Remember(IReadOnlyList<PrView> prs, CancellationToken cancellationToken)
    {
        var kept = await values.Get(KvKeys.EndedPrs, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        var added = EndedPrs.Added(kept.Value, prs);
        var stored = EndedPrs.Merge(kept.Value, prs) is { } ended ? await values.Set(KvKeys.EndedPrs, ended, cancellationToken).ConfigureAwait(false) : Results.Ok();
        return stored.IsError ? stored.Error : Results.Success(added);
    }
}
