using System.Globalization;
using System.Net;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Queries honour X-Sindri-After: they wait for the views to reach it, 503 SND-API-011 after 2 s (API spec §2.4, plan G6).</summary>
public sealed class ReadYourWritesTests(CallerHost host) : IClassFixture<CallerHost>
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_query_after_the_ledger_head_answers_once_the_views_reach_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var head = (await host.App.Services.GetRequiredService<ILedgerStore>().Head(ct)).Value;

        var asking = host.Http.Ask(Get("/sindri/status", After(head)));
        _ = await host.App.Services.GetRequiredService<Projector>().CatchUp(ct);
        var answer = await asking;

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_query_after_a_sequence_the_views_never_reach_is_503_SND_API_011_naming_both()
    {
        var head = (await host.App.Services.GetRequiredService<ILedgerStore>().Head(TestContext.Current.CancellationToken)).Value;

        var answer = await host.Http.Ask(Get("/sindri/status", After(head + 1000)));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-API-011"), (answer.Status, answer.Code));
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"waiting for {head + 1000}"), answer.Body, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("soon")]
    [InlineData("-1")]
    [InlineData("")]
    public async Task A_malformed_X_Sindri_After_is_400_SND_CLI_003(string value)
    {
        var answer = await host.Http.Ask(Get("/sindri/status", ("X-Sindri-After", value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (answer.Status, answer.Code));
    }

    private static (string, string) After(long sequence) => ("X-Sindri-After", sequence.ToString(CultureInfo.InvariantCulture));
}
