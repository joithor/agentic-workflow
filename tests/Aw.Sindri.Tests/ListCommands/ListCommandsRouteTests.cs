using System.Net;
using System.Net.Http.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ListCommands;

/// <summary>GET /sindri/commands on the real host: a query, so a session (the CLI inside an agent session) reads it too.</summary>
public sealed class ListCommandsRouteTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Commands_route_lists_every_action_for_a_session_caller()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/sindri/commands", UriKind.Relative)).AsSession();
        using var answer = await http.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var body = await answer.Content.ReadFromJsonAsync(HttpJson.Info<ListCommandsResponse>(), ct);
        var catalog = app.Services.GetRequiredService<ICommandCatalog>();
        Assert.Equal(
            catalog.Rows.Select(row => row.Action.Value).Order(StringComparer.Ordinal),
            body!.Rows.Select(row => row.Action.Value),
            StringComparer.Ordinal);
        var stop = Assert.Single(body.Rows, row => row.Action.Value == "StopProcess");
        Assert.Equal((ActionClass.Desk, "ready", true), (stop.Class, stop.Ui, stop.Destructive));
        await app.StopAsync(ct);
    }
}
