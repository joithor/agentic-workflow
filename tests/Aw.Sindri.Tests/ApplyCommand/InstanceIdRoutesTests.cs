using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// Instance ids are opaque (engine spec §6.3): 'WFI-…', minted from the address, returned by start, list and get. A CLI
/// address travels only in the 'address' query of 'GET /workflow/instances'; the instance routes take the id.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InstanceIdRoutesTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_address_with_slashes_resolves_through_the_address_query_and_its_id_drives_the_instance_routes()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment);
        var started = await served.Cli("start", "workflow.step", "--item", "T-50", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"), "--json");
        _ = await served.Until("T-50/step/1", static v => v.Task is not null);

        var (listed, list) = await Send(served, HttpMethod.Get, "/sindri/workflow/instances?address=" + Uri.EscapeDataString("T-50/step/1"));
        var instance = list.GetProperty("instances")[0];
        var id = instance.GetProperty("id").GetString();
        var (issued, nonce) = await Send(served, HttpMethod.Post, $"/sindri/workflow/instances/{id}/nonce");
        var cancel = await served.Cli("cancel", "T-50/step/1");

        Assert.Equal(0, started.Exit);
        Assert.Equal(id, JsonDocument.Parse(started.Stdout).RootElement.GetProperty("instance").GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.OK, listed);
        Assert.Equal(("T-50/step/1", WorkflowInstanceId.Of(InstanceAddress.From("T-50/step/1")).Value), (instance.GetProperty("address").GetString(), id));
        Assert.Equal((HttpStatusCode.OK, "T-50/step/1"), (issued, nonce.GetProperty("instance").GetString()));
        Assert.Equal((0, "  -> cancelled"), (cancel.Exit, cancel.Lines[1]));
    }

    [Fact]
    public async Task An_unknown_id_is_SND_WF_001_and_an_address_in_the_route_is_refused()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment);

        var (unknown, problem) = await Send(served, HttpMethod.Post, $"/sindri/workflow/instances/WFI-{new string('0', 32)}/nonce");
        var (address, _) = await Send(served, HttpMethod.Post, "/sindri/workflow/instances/" + Uri.EscapeDataString("T-51/step/1") + "/nonce");
        var (missing, notFound) = await Send(served, HttpMethod.Get, "/sindri/workflow/instances?address=" + Uri.EscapeDataString("T-51/step/1"));

        Assert.Equal("SND-WF-001", problem.GetProperty("title").GetString());
        Assert.NotEqual(HttpStatusCode.OK, unknown);
        Assert.Equal(HttpStatusCode.BadRequest, address);
        Assert.Equal(("SND-WF-001", false), (notFound.GetProperty("title").GetString(), missing == HttpStatusCode.OK));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(Served served, HttpMethod method, string route)
    {
        using var request = new HttpRequestMessage(method, route).AsHuman(served.App, terminal: true);
        using var answer = await served.Http.SendAsync(request, Token);
        var text = await answer.Content.ReadAsStringAsync(Token);
        return (answer.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }
}
