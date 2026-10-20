using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Setup;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>
/// What the Setup tests share: the setup goldens as the served list (spec-shaped copies of api #458's own, PROVISIONAL until it merges), the catalog as served, a fake clock and the
/// opener the shell would give the card. A step the tests need and no golden has is written as JSON here.
/// </summary>
internal sealed class SetupFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new();

    internal FakeClipboard Clipboard { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(A.Noon);

    internal CatalogViewModel Catalog { get; }

    /// <summary>The actions the card opens in the app, as the shell decides it; none by default.</summary>
    internal HashSet<string> InApp { get; } = new(StringComparer.Ordinal);

    internal SetupFixture(string catalog = "catalog")
    {
        Catalog = new CatalogViewModel(Clipboard, Clock);
        Server.Catalog = RouteAnswer.Api("commands", catalog);
        Catalog.Show(Url, Server, AwJson.Deserialize<CommandList>(System.Text.Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", catalog)), SindriApiClient.Json).Value);
    }

    internal void Serve(string scenario) => Server.SetupAnswer = RouteAnswer.SpecShaped("setup", scenario);

    internal void Serve(JsonNode list) => Server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

    internal static JsonNode Golden(string scenario) => JsonNode.Parse(GoldenFiles.SpecShaped("setup", scenario))!; // why: the golden is a JSON document

    /// <summary>The golden with one step changed: its state and, when given, its action.</summary>
    internal static JsonNode With(string scenario, string step, string state, string? action = null)
    {
        var list = Golden(scenario);
        var served = list["steps"]!.AsArray().Single(s => (string)s!["id"]! == step)!;
        served["state"] = state;
        served["action"] = action;
        return list;
    }

    internal SetupViewModel Setup() => new(Server, Catalog, Clock, InApp.Contains);

    /// <summary>A view model that has read the list once.</summary>
    internal async Task<SetupViewModel> Read(CancellationToken token)
    {
        var setup = Setup();
        await setup.Refresh(Url, token);
        return setup;
    }
}
