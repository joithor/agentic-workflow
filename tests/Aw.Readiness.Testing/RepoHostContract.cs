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
        Expect.Sequence(["area:kernel", "ring:0", "size:S", "type:bug"], (facts.Labels.Names ?? []).Order(StringComparer.Ordinal), "labels");
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
        Expect.Sequence(["bug", "documentation"], (facts.Labels.Names ?? []).Order(StringComparer.Ordinal), "labels");
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
        Expect.True(facts.Labels.Names is null, "labels of an unreachable host must be unknown");
        AssertEvidence(facts);
    }

    /// <summary>Ruleset rules on the default branch count as protection.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Ruleset_rules_count_as_protection()
    {
        var facts = await HostFor(HostScenario.Rulesets).Read(Root, CancellationToken.None);

        Expect.Equal(expected: true, facts.BranchProtection.Holds, "BranchProtection through rulesets");
        AssertEvidence(facts);
    }

    /// <summary>A plan that can't protect branches is a known 'not protected', not unknown.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_plan_that_cannot_protect_is_not_protected()
    {
        var facts = await HostFor(HostScenario.PlanCannotProtect).Read(Root, CancellationToken.None);

        Expect.Equal(expected: false, facts.BranchProtection.Holds, "BranchProtection on a plan without it");
        AssertEvidence(facts);
    }

    /// <summary>A ruleset whose rules protect nothing (no required checks, PRs, force-push or deletion rules) isn't
    /// protection.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_ruleset_without_protective_rules_is_not_protection()
    {
        var facts = await HostFor(HostScenario.WeakRuleset).Read(Root, CancellationToken.None);

        Expect.Equal(expected: false, facts.BranchProtection.Holds, "BranchProtection with only a non-protective rule");
        AssertEvidence(facts);
    }

    /// <summary>A non-admin login can see neither protection nor secret scanning: unknown, not 'off'.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_non_admin_login_leaves_secret_scanning_unknown()
    {
        var facts = await HostFor(HostScenario.NotAdmin).Read(Root, CancellationToken.None);

        Expect.Equal(expected: null, facts.SecretScanning.Holds, "SecretScanning for a non-admin");
        Expect.Equal(expected: null, facts.BranchProtection.Holds, "BranchProtection for a non-admin (GitHub answers 404)");
        AssertEvidence(facts);
    }

    private static void AssertEvidence(HostFacts facts)
    {
        Expect.Evidence(facts.BranchProtection.Evidence, "BranchProtection");
        Expect.Evidence(facts.SecretScanning.Evidence, "SecretScanning");
        Expect.Evidence(facts.Releases.Evidence, "Releases");
        Expect.Evidence(facts.Labels.Evidence, "Labels");
    }
}
