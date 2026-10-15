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
/// The engine's write routes through API P1's command chain (engine spec rev 8 §6.2, §6.3; "Phase 4" signal 5): an
/// agent session is refused start, answer, command, cancel and complete with SND-AUTH-002 naming the action; the app (a
/// human without a terminal) runs the desk ones but not a protected answer; any caller that may command an instance may
/// ask for its nonce.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowRoutesTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private static readonly string _instance = "/sindri/workflow/instances/" + Id("T-31/step/1");

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task An_agent_session_gets_SND_AUTH_002_on_start_answer_command_cancel_and_complete()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);

        (string Route, string Body, string Action)[] calls =
        [
            ("/sindri/workflow/instances", Start("T-31"), "StartInstance"),
            (_instance + "/commands", Command("answer", "\"answer\": \"text\", \"text\": \"go on\""), "ApplyCommand:answer-text"),
            (_instance + "/commands", Command("command", "\"verb\": \"skip\""), "ApplyCommand:command"),
            (_instance + "/commands", Command("cancel", string.Empty), "ApplyCommand:cancel"),
            (_instance + "/runs/1.1/complete", Complete(), "CompleteStep"),
        ];
        var refused = new List<(HttpStatusCode, string?, bool)>();
        foreach (var (route, body, action) in calls)
        {
            var (status, title, detail) = await Post(served, route, body, static r => r.AsSession());
            refused.Add((status, title, detail?.StartsWith(action + " is a human decision", StringComparison.Ordinal) == true));
        }

        Assert.All(refused, static r => Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002", true), r));
        Assert.Empty(await served.Events("T-31/step/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task The_app_starts_an_instance_but_a_grant_is_SND_AUTH_002()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);

        var (started, _, startDetail) = await Post(served, "/sindri/workflow/instances", Start("T-32"), r => r.AsHuman(served.App, terminal: false));
        _ = await served.Until("T-32/step/1", static v => v.Task is not null);
        var (granted, title, detail) = await Post(served, "/sindri/workflow/instances/" + Id("T-32/step/1") + "/commands",
            Command("answer", "\"answer\": \"grant\", \"amount\": 1"), r => r.AsHuman(served.App, terminal: false));

        Assert.True(started == HttpStatusCode.OK, startDetail);
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (granted, title));
        Assert.StartsWith("ApplyCommand:answer-grant is a human decision. Ask the human to run it in a terminal", detail, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_agent_session_may_ask_for_a_nonce_for_an_instance()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.step", "--item", "T-33", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-33/step/1", static v => v.Task is not null);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/sindri/workflow/instances/{Id("T-33/step/1")}/nonce").AsSession();
        using var answer = await served.Http.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, body);
        Assert.Matches("^[0-9a-f]{32}$", JsonDocument.Parse(body).RootElement.GetProperty("nonce").GetString());
    }

    private static string Start(string item) =>
        $$"""{ "recipe": "workflow.step", "item": "{{item}}", "nextAttempt": false, "facts": { "fact:spec": { "name": "spec.md", "content": "{{Convert.ToBase64String("the spec"u8)}}" } } }""";

    private static string Command(string kind, string fields) =>
        $$"""{ "instance": "T-31/step/1", "kind": "{{kind}}"{{(fields.Length == 0 ? string.Empty : ", " + fields)}} }""";

    private static string Complete() =>
        """{ "instance": "T-31/step/1", "run": "1.1", "outcome": "done", "outputs": {} }""";

    private static string Id(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;

    private static async Task<(HttpStatusCode Status, string? Title, string? Detail)> Post(Served served, string route, string body, Func<HttpRequestMessage, HttpRequestMessage> caller)
    {
        using var request = caller(new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        using var answer = await served.Http.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (answer.IsSuccessStatusCode || text.Length == 0)
        {
            return (answer.StatusCode, null, text);
        }

        var problem = JsonDocument.Parse(text).RootElement;
        return (answer.StatusCode, problem.TryGetProperty("title", out var t) ? t.GetString() : null, problem.TryGetProperty("detail", out var d) ? d.GetString() : text);
    }
}
