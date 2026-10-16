using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.RunService;

/// <summary>RunService, PauseService, ResumeService and CancelJob on the real host and a real SQLite ledger (API P6, S4): a session, or a call with no caller header, is refused 403 SND-AUTH-002 and only command.handled is written.</summary>
public sealed class ServiceCommandRefusalTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    public static TheoryData<string, bool> Routes() => new()
    {
        { "/sindri/services/heartbeat/run", true },
        { "/sindri/services/heartbeat/run", false },
        { "/sindri/services/heartbeat/pause", true },
        { "/sindri/services/heartbeat/resume", true },
        { "/sindri/jobs/JOB-00000000000000000000000000000001/cancel", true },
    };

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Routes))]
    public async Task A_session_or_a_call_with_no_caller_header_is_refused_and_only_command_handled_is_written(string route, bool asSession)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await Started(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        _ = asSession ? request.AsSession() : request;

        using var answer = await http.SendAsync(request, ct);
        var title = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("title").GetString();
        await app.StopAsync(ct);

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (answer.StatusCode, title));
        Assert.Equal([EventTypes.ProcessStarted, EventTypes.CommandHandled], await Types(ct), StringComparer.Ordinal);
    }

    private async Task<Microsoft.AspNetCore.Builder.WebApplication> Started(CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);   // the Temporal side is faked: these tests are about the API
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).IsSuccess);
        }

        await app.StartAsync(ct);
        return app;
    }

    private async Task<IReadOnlyList<string>> Types(CancellationToken ct) =>
        [.. (await SqliteStores.Create(_home.Path, TimeProvider.System, new FakeProcessProbe()).Ledger.ReadAfter(0, 100, ct)).Value.Select(e => e.Envelope.Type)];
}
