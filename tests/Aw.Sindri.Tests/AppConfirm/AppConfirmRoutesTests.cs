using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.ApiGoldens.ApproveAllScenarios;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>The app's confirmation on the real host (setup-once spec §4): the human token with no terminal issues an app challenge and redeems it for the profile and approve-all; a session never does.</summary>
public sealed class AppConfirmRoutesTests
{
    private const string AppChallenge = "/sindri/workflow/challenges/app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The Sindri app: the human token, the 'ui' label and no terminal claim.</summary>
    private static (string Name, string Value)[] App(GoldenHost host) =>
        [(SindriHeaders.Caller, "human"), (SindriHeaders.Client, "ui"), (SindriHeaders.Token, host.Token.Value)];

    private static (string Name, string Value)[] Session => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")];

    private static void After(GoldenHost host, TimeSpan time) => ((FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>()).Advance(time);

    private static async Task<GoldenHost> Started(FakeShippedRecipes? shipped = null, bool approved = true)
    {
        var host = await Start(shipped ?? new FakeShippedRecipes(ApproveAllFixtures.StepAndItem()), Token);
        if (approved)
        {
            await ViewScenarios.Approve(host, Token);
        }

        return host;
    }

    private static async Task<JsonElement> Issue(GoldenHost host, string action)
    {
        var issued = await host.Http.Ask(Post(AppChallenge, $$"""{"action":"{{action}}"}""", App(host)));
        Assert.True(issued.Status == HttpStatusCode.OK, issued.Body);
        return JsonDocument.Parse(issued.Body).RootElement;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_approves_the_profile_with_a_typed_challenge()
    {
        await using var host = await Started(approved: false);
        var hash = (await host.LiveHash(Token)).Value;
        var action = "approve profile " + hash[..6];
        var issued = await Issue(host, action);
        After(host, IssuedChallenge.EarliestAnswer);

        var approved = await host.Http.Ask(Post("/sindri/profile/approve",
            $$$"""{"hash":"{{{hash}}}","answer":{"challenge":"{{{issued.GetProperty("challenge").GetString()}}}","typed":"{{{action}}}"}}""", App(host)));

        Assert.Equal(("typed", HttpStatusCode.OK), (issued.GetProperty("confirm").GetString(), approved.Status));
        Assert.True(JsonDocument.Parse(approved.Body).RootElement.GetProperty("approved").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_approves_the_plan_with_a_hold_and_a_short_hold_or_an_early_answer_leaves_the_challenge_open()
    {
        await using var host = await Started();
        var issued = await Issue(host, "approve-all " + await Digest(host));
        var challenge = issued.GetProperty("challenge").GetString();
        string Body(long held) => string.Create(CultureInfo.InvariantCulture, $$"""{"challenge":"{{challenge}}","heldMs":{{held}}}""");

        var early = await host.Http.Ask(Post(PlanRoute, Body(IssuedChallenge.MinHoldMs), App(host)));
        After(host, IssuedChallenge.EarliestAnswer);
        var shortHold = await host.Http.Ask(Post(PlanRoute, Body(IssuedChallenge.MinHoldMs - 1), App(host)));
        var started = await host.Http.Ask(Post(PlanRoute, Body(IssuedChallenge.MinHoldMs), App(host)));
        await JobHost.Settle(host, Token);

        Assert.Equal(("held", 1500), (issued.GetProperty("confirm").GetString(), issued.GetProperty("minHoldMs").GetInt32()));
        Assert.Equal([(HttpStatusCode.Forbidden, "SND-SETUP-003"), (HttpStatusCode.Forbidden, "SND-SETUP-003"), (HttpStatusCode.OK, null)],
            new[] { early, shortHold, started }.Select(static a => (a.Status, a.Code)));
        var again = await host.Http.Ask(Get(PlanRoute, Human(host.Token.Value)));
        Assert.Empty(JsonDocument.Parse(again.Body).RootElement.GetProperty("recipes").EnumerateArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_also_redeems_approve_all_by_typing_its_text()
    {
        await using var host = await Started();
        var issued = await Issue(host, "approve-all " + await Digest(host));
        After(host, IssuedChallenge.EarliestAnswer);

        var started = await host.Http.Ask(Post(PlanRoute,
            $$"""{"challenge":"{{issued.GetProperty("challenge").GetString()}}","typed":"{{issued.GetProperty("typedAlternative").GetString()}}"}""", App(host)));

        Assert.Equal(HttpStatusCode.OK, started.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_is_refused_the_app_challenge_and_each_approval()
    {
        await using var host = await Started();
        var hash = (await host.LiveHash(Token)).Value;

        var challenge = await host.Http.Ask(Post(AppChallenge, """{"action":"approve-all c41e07"}""", Session));
        var profile = await host.Http.Ask(Post("/sindri/profile/approve", $$"""{"hash":"{{hash}}"}""", Session));
        var all = await host.Http.Ask(Post(PlanRoute, """{"challenge":"CHL-00000000000000000000000000000000","heldMs":5000}""", Session));
        var recipe = await host.Http.Ask(Post("/sindri/workflow/recipes/workflow.step@" + new string('a', 64) + "/approve", """{"challenge":"CHL-00000000000000000000000000000000","typed":"x"}""", Session));

        Assert.Equal([(HttpStatusCode.Forbidden, "SND-AUTH-002")], new[] { challenge, profile, all, recipe }.Select(static a => (a.Status, a.Code)).Distinct().ToArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_is_still_refused_the_terminal_challenge_route_and_every_other_action()
    {
        await using var host = await Started();

        var terminal = await host.Http.Ask(Post("/sindri/workflow/challenges", """{"action":"approve-all c41e07"}""", App(host)));
        var revoke = await host.Http.Ask(Post(AppChallenge, """{"action":"revoke workflow.step@9ab2d40c1e77"}""", App(host)));
        var stop = await host.Http.Ask(Post(AppChallenge, """{"action":"stop"}""", App(host)));

        Assert.Equal([(HttpStatusCode.Forbidden, "SND-AUTH-002")], new[] { terminal, revoke, stop }.Select(static a => (a.Status, a.Code)).Distinct().ToArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_all_before_a_profile_is_approved_has_no_app_challenge()
    {
        await using var host = await Started(approved: false);

        var challenge = await host.Http.Ask(Post(AppChallenge, """{"action":"approve-all c41e07"}""", App(host)));

        Assert.Equal((HttpStatusCode.Conflict, "SND-SETUP-007"), (challenge.Status, challenge.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_hold_cannot_redeem_a_typed_challenge()
    {
        await using var host = await Started(approved: false);
        var hash = (await host.LiveHash(Token)).Value;
        var issued = await Issue(host, "approve profile " + hash[..6]);
        After(host, IssuedChallenge.EarliestAnswer);

        var held = await host.Http.Ask(Post("/sindri/profile/approve",
            $$$"""{"hash":"{{{hash}}}","answer":{"challenge":"{{{issued.GetProperty("challenge").GetString()}}}","heldMs":5000}}""", App(host)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-SETUP-003"), (held.Status, held.Code));
    }
}
