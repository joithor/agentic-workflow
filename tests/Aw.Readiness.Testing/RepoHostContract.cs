using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The contract every <see cref="IRepoHost"/> passes (the fake and the gh adapter over a stub gh): known facts
/// come back as true or false with evidence, and a host that can't be reached is unknown, never a guess or a throw.</summary>
public abstract class RepoHostContract
{
    /// <summary>The instant the scenarios' release dates are relative to.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A host that answers as the scenario says.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The host.</returns>
    protected abstract IRepoHost HostFor(HostScenario scenario);

    /// <summary>The checkout root the host is asked about (an adapter that runs in it needs a real directory).</summary>
    protected virtual string Root => "/repo";

    /// <summary>The ready scenario's facts all hold.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_ready_repo_reports_protection_scanning_and_releases()
    {
        var facts = await HostFor(HostScenario.Ready).Read(Root, CancellationToken.None);

        Expect.Equal(expected: true, facts.BranchProtection.Holds, "BranchProtection");
        Expect.Equal(expected: true, facts.SecretScanning.Holds, "SecretScanning");
        Expect.Equal(3, facts.Releases.PublishedAt?.Count(p => p > Now.AddDays(-90)), "releases in the last 90 days");
        AssertEvidence(facts);
    }

    /// <summary>The bare scenario's facts are known and false.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_bare_repo_reports_no_protection_no_scanning_and_no_releases()
    {
        var facts = await HostFor(HostScenario.Bare).Read(Root, CancellationToken.None);

        Expect.Equal(expected: false, facts.BranchProtection.Holds, "BranchProtection");
        Expect.Equal(expected: false, facts.SecretScanning.Holds, "SecretScanning");
        Expect.Equal(0, facts.Releases.PublishedAt?.Count, "releases");
        AssertEvidence(facts);
    }

    /// <summary>An unreachable host makes every fact unknown, with the reason.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreachable_host_is_unknown_not_a_failure()
    {
        var facts = await HostFor(HostScenario.Unreachable).Read(Root, CancellationToken.None);

        Expect.Equal(expected: null, facts.BranchProtection.Holds, "BranchProtection of an unreachable host");
        Expect.Equal(expected: null, facts.SecretScanning.Holds, "SecretScanning of an unreachable host");
        Expect.True(facts.Releases.PublishedAt is null, "releases of an unreachable host must be unknown");
        AssertEvidence(facts);
    }

    private static void AssertEvidence(HostFacts facts)
    {
        Expect.Evidence(facts.BranchProtection.Evidence, "BranchProtection");
        Expect.Evidence(facts.SecretScanning.Evidence, "SecretScanning");
        Expect.Evidence(facts.Releases.Evidence, "Releases");
    }
}
