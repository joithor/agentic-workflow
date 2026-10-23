using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// PB spec §13 (PB-E): the babysitter's 'merge' and 'rework' are recipe verbs of 'command', a desk row, so an agent session that sends either gets 403 SND-AUTH-002 from the route and
/// exit 2 'SND-AUTH-002 ' from the CLI, and nothing is recorded; the app's human is not refused by authorization (the engine then answers about the instance, not about the caller).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class BabysitterVerbsRefusedTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Theory(Timeout = 120_000)]
    [InlineData("merge")]
    [InlineData("rework")]
    public async Task A_session_sending_a_babysitter_verb_is_403_SND_AUTH_002_and_records_nothing(string verb)
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served);
        var before = await served.Events(address);

        var (status, problem) = await Post(served, address, verb, static r => r.AsSession());

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("SND-AUTH-002", problem.GetProperty("title").GetString());
        Assert.StartsWith("ApplyCommand:command is a human decision", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(before, await served.Events(address));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("merge")]
    [InlineData("rework")]
    public async Task The_CLI_run_by_a_session_exits_2_with_SND_AUTH_002_and_records_nothing(string verb)
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served);
        var before = await served.Events(address);

        var run = await served.AsSessionIn("agent-session", "demo", "command", address, verb);

        Assert.Equal(2, run.Exit);
        Assert.StartsWith("SND-AUTH-002 ", run.Stderr, StringComparison.Ordinal);
        Assert.Equal(before, await served.Events(address));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("merge")]
    [InlineData("rework")]
    public async Task The_apps_human_is_not_refused_by_authorization(string verb)
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served);

        var (status, problem) = await Post(served, address, verb, r => r.AsHuman(served.App, terminal: false));

        Assert.NotEqual(HttpStatusCode.Forbidden, status);
        Assert.False(problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("title", out var title) && string.Equals(title.GetString(), "SND-AUTH-002", StringComparison.Ordinal));
    }

    // An instance on an item in the repo "demo": a session working there can see it, so its refusal is the authorization's and not a 404.
    private async Task<string> Started(Served served)
    {
        var item = await HandbackShortFormTests.ItemIn(served, "demo");
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);
        return address;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Problem)> Post(Served served, string address, string verb, Func<HttpRequestMessage, HttpRequestMessage> caller)
    {
        var route = "/sindri/workflow/instances/" + WorkflowInstanceId.Of(InstanceAddress.From(address)).Value + "/commands";
        var body = $$"""{ "instance": "{{address}}", "kind": "command", "verb": "{{verb}}" }""";
        using var request = caller(new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        using var answer = await served.Http.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (answer.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }
}
