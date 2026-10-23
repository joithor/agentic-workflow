namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'pr-watch.json' holds (PR babysitter spec §6.2): the last digest and the times of each watched pull request, and each host's back-off.</summary>
/// <param name="PolledAt">When the watcher last polled (it polls every 'workflow.pr.pollInterval').</param>
/// <param name="Hosts">The back-off by host.</param>
/// <param name="Pulls">The record of each watched pull request, by its link exactly as 'fact:pr' holds it.</param>
internal sealed record PrWatchFile(DateTimeOffset? PolledAt, Dictionary<string, HostBackoff> Hosts, Dictionary<string, WatchedRecord> Pulls)
{
    /// <summary>A file with nothing recorded.</summary>
    internal static PrWatchFile Empty => new(PolledAt: null, new Dictionary<string, HostBackoff>(StringComparer.Ordinal), new Dictionary<string, WatchedRecord>(StringComparer.Ordinal));
}
