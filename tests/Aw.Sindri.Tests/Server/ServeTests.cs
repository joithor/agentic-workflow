using System.Net;
using System.Net.Http.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>The control plane on a real loopback port with real adapters in a temp home, through the pipeline Program runs.</summary>
[Collection(nameof(ProcessEnvironment))]
public sealed class ServeTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_api_serves_status_ping_events_and_stop_under_the_lease()
    {
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var started = await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), Token);
            Assert.True(started.IsSuccess);
        }

        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var status = await http.GetFromJsonAsync("/sindri/status", HttpJson.Info<GetStatusResponse>(), Token);
        var ping = await http.GetFromJsonAsync("/sindri/ping", HttpJson.Info<Ping>(), Token);
        var events = await FirstEventLines(http);
        var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, Token);

        Assert.True(status!.Running);
        Assert.Equal((Environment.ProcessId, status.Epoch), (ping!.Pid, ping.Epoch));
        Assert.Contains("event: process.started", events, StringComparison.Ordinal);
        Assert.Contains("id: 1", events, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Null(app.Services.GetRequiredService<HeldLease>().Current);
        Assert.True(app.Services.GetRequiredService<ProcessShutdown>().Stopping.IsCancellationRequested);
        await app.StopAsync(Token);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_approve_body_is_a_400_problem_with_no_stack_trace()
    {
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var body = new StringContent("""{"hash":"nope","confirmed":true}""", System.Text.Encoding.UTF8, "application/json");
        var answer = await http.PostAsync(new Uri("/sindri/profile/approve", UriKind.Relative), body, Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.DoesNotContain("   at ", await answer.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        await app.StopAsync(Token);
    }

    private static async Task<string> FirstEventLines(HttpClient http)
    {
        using var cut = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cut.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await http.GetAsync(new Uri("/sindri/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cut.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cut.Token));
        var lines = new List<string>();
        while (lines.Count < 3 && await reader.ReadLineAsync(cut.Token) is { } line)
        {
            lines.Add(line);
        }

        return string.Join('\n', lines);
    }
}
