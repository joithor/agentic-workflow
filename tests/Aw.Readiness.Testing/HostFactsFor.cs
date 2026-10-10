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
            new HostReleases([RepoHostContract.Now.AddDays(-1), RepoHostContract.Now.AddDays(-30), RepoHostContract.Now.AddDays(-60)], "3 releases")),
        HostScenario.Bare => new HostFacts(
            new HostFact(Holds: false, Evidence: "main is not protected"),
            new HostFact(Holds: false, Evidence: "secret scanning disabled"),
            new HostReleases([], "0 releases")),
        HostScenario.Unreachable => HostFacts.Unknown("host unreachable (test)"),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, message: null),
    };
}
