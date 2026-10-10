namespace Aw.Readiness.Core.Ports;

/// <summary>Reads facts about the repo from its host (the GitHub port). Read-only.</summary>
public interface IRepoHost
{
    /// <summary>Reads the host facts for the checkout at a root. Never throws: anything unreadable is unknown.</summary>
    /// <param name="root">The repo root (the host repo is the checkout's).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts.</returns>
    public ValueTask<HostFacts> Read(string root, CancellationToken cancellationToken);
}
