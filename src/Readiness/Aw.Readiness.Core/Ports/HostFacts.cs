namespace Aw.Readiness.Core.Ports;

/// <summary>What the repo host (GitHub) reports about the repo. Anything it couldn't read is unknown, never a guess.</summary>
/// <param name="BranchProtection">Whether the default branch is protected (branch protection or a ruleset).</param>
/// <param name="SecretScanning">Whether secret scanning and push protection are on.</param>
/// <param name="Releases">The repo's releases.</param>
public sealed record HostFacts(HostFact BranchProtection, HostFact SecretScanning, HostReleases Releases)
{
    /// <summary>Every fact unknown, for the same reason (offline, --no-remote, no host CLI).</summary>
    /// <param name="reason">Why nothing could be read.</param>
    /// <returns>The facts.</returns>
    public static HostFacts Unknown(string reason) =>
        new(new HostFact(Holds: null, Evidence: reason), new HostFact(Holds: null, Evidence: reason), new HostReleases(PublishedAt: null, Evidence: reason));
}
