using System.Net;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The item filter of GET /sindri/events and /sindri/ledger on a host whose projector never runs (the views stay empty): an item named
/// by id resolves through the ledger, so scoping never depends on view freshness (API P5, CC2-2), and an unknown item is refused
/// as a plain 404 before the stream sends a frame.
/// </summary>
public sealed class EventItemFilterTests
{
    private const string Unknown = "ITM-00000000000000000000000000000001";

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Item_filter_by_id_works_while_the_views_are_empty_on_the_ledger()
    {
        await using var host = await HostWithOneItem(TestContext.Current.CancellationToken);
        var id = await Created(host);

        var answer = await host.Http.Ask(Get("/sindri/ledger?item=" + id, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Contains("item.created", answer.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Item_filter_by_id_works_while_the_views_are_empty_on_the_stream()
    {
        await using var host = await HostWithOneItem(TestContext.Current.CancellationToken);
        var id = await Created(host);

        await using var stream = await EventStreamClient.Open(host, "/sindri/events?item=" + id);

        Assert.Equal("event: item.created", await stream.Line());
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [InlineData("/sindri/events?item=" + Unknown)]
    [InlineData("/sindri/events?item=ENG-999")]
    public async Task An_unknown_item_on_events_is_a_404_before_any_frame(string route)
    {
        await using var host = await HostWithOneItem(TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Get(route, Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (answer.Status, answer.Code));
    }

    private static async Task<GoldenHost> HostWithOneItem(CancellationToken ct)
    {
        var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, ct);
        return host;
    }

    private static async Task<string> Created(GoldenHost host) => await ItemScenarios.Create(host, "A moment ago", "demo");
}
