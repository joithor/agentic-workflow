using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The facts each <see cref="HostScenario"/> stands for, for the fake host.</summary>
public static class HostFactsFor
{
    /// <summary>The scenario's facts.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The facts.</returns>
    public static HostFacts Scenario(HostScenario scenario) => scenario switch
    {
        HostScenario.Ready => new HostFacts(
            new HostFact(Holds: true, Evidence: "ruleset rules on main"),
            new HostFact(Holds: true, Evidence: "secret scanning and push protection enabled"),
            new HostReleases([RepoHostContract.Now.AddDays(-1), RepoHostContract.Now.AddDays(-30), RepoHostContract.Now.AddDays(-60)], "3 releases"),
            new HostLabels(["type:bug", "area:kernel", "size:S", "ring:0"], "4 labels")),
        HostScenario.Rulesets => Scenario(HostScenario.Ready) with { BranchProtection = new HostFact(Holds: true, Evidence: "ruleset rules apply to main: pull_request") },
        HostScenario.PlanCannotProtect => Scenario(HostScenario.Bare) with { BranchProtection = new HostFact(Holds: false, Evidence: "main can't be protected on this plan (HTTP 403)") },
        HostScenario.NotAdmin => Scenario(HostScenario.Ready) with
        {
            BranchProtection = new HostFact(Holds: null, Evidence: "protection isn't readable by a non-admin"),
            SecretScanning = new HostFact(Holds: null, Evidence: "not reported to a non-admin"),
        },
        HostScenario.WeakRuleset => Scenario(HostScenario.Bare) with { BranchProtection = new HostFact(Holds: false, Evidence: "only copilot_code_review") },
        HostScenario.Bare => new HostFacts(
            new HostFact(Holds: false, Evidence: "main is not protected"),
            new HostFact(Holds: false, Evidence: "secret scanning disabled"),
            new HostReleases([], "0 releases"),
            new HostLabels(["bug", "documentation"], "2 labels")),
        HostScenario.Unreachable => HostFacts.Unknown("host unreachable (test)"),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, message: null),
    };
}
