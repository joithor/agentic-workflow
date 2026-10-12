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

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The chain on the real host and a real SQLite ledger: a session's approve is refused, and only its audit row is written.</summary>
public sealed class SessionApproveTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Session_approve_is_403_and_only_command_handled_is_written()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).IsSuccess);
        }

        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        var hash = SliceHarness.Profile().Hash.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/sindri/profile/approve")
        {
            Content = new StringContent($$"""{"hash":"{{hash}}"}""", Encoding.UTF8, "application/json"),
        }.AsSession();

        using var answer = await http.SendAsync(request, ct);
        var title = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("title").GetString();
        await app.StopAsync(ct);

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (answer.StatusCode, title));
        var events = (await SqliteStores.Create(_home.Path, TimeProvider.System, new FakeProcessProbe()).Ledger.ReadAfter(0, 100, ct)).Value;
        Assert.Equal([EventTypes.ProcessStarted, EventTypes.CommandHandled], events.Select(e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.Equal("session:cli", events[1].Envelope.Actor);
        Assert.Contains("\"outcome\":\"refused\"", events[1].Envelope.Payload, StringComparison.Ordinal);
    }
}
