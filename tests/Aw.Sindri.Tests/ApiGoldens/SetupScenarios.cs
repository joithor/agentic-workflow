using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Ports;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'GET /setup' (the setup list): the route's body for each way the six steps can stand. The sources are the set facts of <see cref="InMemorySetupFacts"/>, because the real
/// ones read this machine (Docker, a Claude Code config); the real adapters are tested apart (SetupFactsTests, the served-route test of this group).
/// </summary>
internal static class SetupScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("setup", "fresh"), ("setup", "profile-needs-approval"), ("setup", "running-progress"), ("setup", "failed"), ("setup", "all-done"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts a host whose setup sources answer the scenario's facts.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var facts = Facts(scenario);
        return GoldenHost.Start(temporary: true, ct, extra: services => services.Replace(ServiceDescriptor.Singleton<ISetupFacts>(facts)));
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        _ = ct;
        _ = slug;
        _ = scenario;
        return (await host.Http.Ask(Get("/sindri/setup", Human(host.Token.Value)))).Body;
    }

    private static InMemorySetupFacts Facts(string scenario) => scenario switch
    {
        "profile-needs-approval" => new InMemorySetupFacts
        {
            RepoAnswer = new RepoFact(1),
            ProfileAnswer = new ProfileFact(ProfilePresence.Unapproved),
            RecipesAnswer = new RecipesFact(26, 0),
        },
        "running-progress" => new InMemorySetupFacts
        {
            RepoAnswer = new RepoFact(1),
            ProfileAnswer = new ProfileFact(ProfilePresence.Approved),
            RecipesAnswer = new RecipesFact(26, 26),
            McpAnswer = new McpFact(1, 1),
            MapAnswer = new MapFact(MapPhase.Building, new SetupProgress(40, 100)),
        },
        "failed" => new InMemorySetupFacts
        {
            RepoAnswer = new RepoFact(1),
            ProfileAnswer = new ProfileFact(ProfilePresence.Approved),
            RecipesAnswer = new RecipesFact(26, 26),
            McpAnswer = new McpFact(1, 1),
            MapAnswer = new MapFact(MapPhase.Built),
            OnyxAnswer = new OnyxFact(OnyxState.NotSetUp, Unreachable: false, BlockedByCode: "SND-ONYX-001"),
        },
        "all-done" => InMemorySetupFacts.AllDone(),
        _ => new InMemorySetupFacts(),
    };
}
