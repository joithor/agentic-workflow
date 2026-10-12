using System.Net;
using Aw.Sindri.Core.Lease;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Commands refuse any Origin header with SND-API-008 before their body is bound; queries keep S1's loopback guard (API spec §6, C3, plan G21).</summary>
public sealed class CommandOriginTests(CallerHost host) : IClassFixture<CallerHost>
{
    private static readonly (string, string) _localPage = ("Origin", "http://localhost:3000");

    [Fact, Trait("Lane", "integration")]
    public async Task Command_with_an_Origin_header_is_403_SND_API_008()
    {
        var answer = await host.Http.Ask(Post("/sindri/stop", json: null, [.. Human(host.Token.Value), _localPage]));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (answer.Status, answer.Code));
        Assert.NotNull(host.App.Services.GetRequiredService<HeldLease>().Current);   // why: the stop never ran
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_malformed_command_body_with_an_Origin_is_still_403_SND_API_008()
    {
        var answer = await host.Http.Ask(Post("/sindri/profile/approve", """{"hash":""", _localPage));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (answer.Status, answer.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Query_with_a_loopback_Origin_still_answers()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", _localPage));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_non_loopback_Origin_keeps_the_loopback_guards_bare_403()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", ("Origin", "https://example.com")));

        Assert.Equal((HttpStatusCode.Forbidden, ""), (answer.Status, answer.Body));
    }
}
