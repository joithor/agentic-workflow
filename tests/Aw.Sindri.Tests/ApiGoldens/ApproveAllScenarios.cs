using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of approve-all (setup-once spec §8): 'GET /workflow/recipes/approve-all' over a shipped set the scenario seeds into the fake, and the result the 'ApproveAllRecipes' job
/// keeps after one hold. The set is the stand-in for the engine's: the same bodies must come from its implementation of the port.
/// </summary>
internal static class ApproveAllScenarios
{
    internal const string Slug = "workflow-recipes-approve-all";

    /// <summary>The app's challenge route (setup-once spec §4): 'POST /workflow/challenges/app'.</summary>
    internal const string AppSlug = "workflow-challenges-app";

    /// <summary>The needs-you list's read-time rows (setup-once spec §7.3): 'setup:recipes' for the hold and 'recipe:&lt;id&gt;' for a workflow approved on its own.</summary>
    internal const string NeedsYouSlug = "needs-you";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
        [(Slug, "plan"), (Slug, "plan-first-approval"), (Slug, "applied"), (Slug, "refused"), (Slug, "empty"),
            (AppSlug, "profile"), (AppSlug, "approve-all"), (AppSlug, "refused-action"), (AppSlug, "before-profile"),
            (NeedsYouSlug, "setup-recipes"), (NeedsYouSlug, "recipe")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host over its shipped set.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) => Start(Shipped(scenario), ct);

    /// <summary>Starts a golden host whose shipped recipes are <paramref name="shipped"/>.</summary>
    public static Task<GoldenHost> Start(FakeShippedRecipes shipped, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services => services.Replace(ServiceDescriptor.Singleton<IShippedRecipes>(shipped)));

    /// <summary>The scenario's body: the plan, or the result of the job one hold starts.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(slug);
        if (string.Equals(slug, AppSlug, StringComparison.Ordinal))
        {
            return await AppChallenge(host, scenario, ct);
        }

        await ViewScenarios.Approve(host, ct);
        if (string.Equals(slug, NeedsYouSlug, StringComparison.Ordinal))
        {
            return (await host.Http.Ask(Get("/sindri/needs-you", Human(host.Token.Value)))).Body;
        }

        if (string.Equals(scenario, "applied", StringComparison.Ordinal) || string.Equals(scenario, "refused", StringComparison.Ordinal))
        {
            var job = await Hold(host, ct);
            await JobHost.Settle(host, ct);
            return JsonDocument.Parse((await JobHost.Read(host, "/sindri/jobs/" + job)).Body).RootElement.GetProperty("result").GetRawText();
        }

        return (await host.Http.Ask(Get(PlanRoute, Human(host.Token.Value)))).Body;
    }

    private static async Task<string> AppChallenge(GoldenHost host, string scenario, CancellationToken ct)
    {
        (string Name, string Value)[] app = [(SindriHeaders.Caller, "human"), (SindriHeaders.Client, "ui"), (SindriHeaders.Token, host.Token.Value)];
        if (new[] { "approve-all", "refused-action" }.Contains(scenario, StringComparer.Ordinal))
        {
            await ViewScenarios.Approve(host, ct);
        }

        var (headersOf, action) = scenario switch
        {
            "profile" => (app, "approve profile " + (await host.LiveHash(ct)).Value[..6]),
            "approve-all" => (app, "approve-all " + await Digest(host)),
            "refused-action" => (app, "revoke workflow.step@9ab2d40c1e77"),
            "before-profile" => (app, "approve-all c41e07"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such challenges-app scenario"),
        };
        return (await host.Http.Ask(Post("/sindri/workflow/challenges/app", $$"""{"action":"{{action}}"}""", headersOf))).Body;
    }

    internal const string PlanRoute = "/sindri/workflow/recipes/approve-all";

    /// <summary>The plan's digest as the app reads it.</summary>
    internal static async Task<string> Digest(GoldenHost host)
    {
        var plan = await host.Http.Ask(Get(PlanRoute, Human(host.Token.Value)));
        Assert.True(plan.Status == HttpStatusCode.OK, plan.Body);
        return JsonDocument.Parse(plan.Body).RootElement.GetProperty("digest").GetString()!;   // why: a plan always carries its digest
    }

    /// <summary>The challenge and the text that redeems it, as the JSON body of the POST.</summary>
    internal static async Task<string> Answer(GoldenHost host, string digest)
    {
        var issued = await host.Http.Ask(Post("/sindri/workflow/challenges", $$"""{"action":"approve-all {{digest}}"}""", Human(host.Token.Value)));
        Assert.True(issued.Status == HttpStatusCode.OK, issued.Body);
        var root = JsonDocument.Parse(issued.Body).RootElement;
        return $$"""{"challenge":"{{root.GetProperty("challenge").GetString()}}","typed":"{{root.GetProperty("action").GetString()}}"}""";   // why: an issued challenge always carries both
    }

    /// <summary>Holds: issues the challenge for the plan as it is and POSTs it as a human at a terminal.</summary>
    /// <returns>The job's id.</returns>
    internal static async Task<string> Hold(GoldenHost host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = await host.Http.Ask(Post(PlanRoute, await Answer(host, await Digest(host)), Human(host.Token.Value)));
        Assert.True(started.Status == HttpStatusCode.OK, started.Body);   // why: every job-class route answers JobStarted as a 200, as the other jobs do
        return JsonDocument.Parse(started.Body).RootElement.GetProperty("jobId").GetString()!;   // why: a started job answers its id
    }

    private static FakeShippedRecipes Shipped(string scenario)
    {
        var shipped = new FakeShippedRecipes(scenario switch
        {
            "plan-first-approval" => ApproveAllFixtures.FirstShip(),
            "refused" => ApproveAllFixtures.OneRefused(),
            "empty" or "recipe" => [],
            _ => ApproveAllFixtures.StepAndItem(),
        });
        if (string.Equals(scenario, "plan", StringComparison.Ordinal) || string.Equals(scenario, "recipe", StringComparison.Ordinal))
        {
            shipped.OwnApproval.Add(ApproveAllFixtures.ShipNeedsItsOwn());
        }

        return shipped;
    }
}
