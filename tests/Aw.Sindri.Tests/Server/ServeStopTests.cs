using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>'sindri serve' as the CLI runs it, stopped through the API while a client follows the event stream.</summary>
public sealed class ServeStopTests : IDisposable
{
    private static readonly TimeSpan _prompt = TimeSpan.FromSeconds(5);   // well under Kestrel's 30 s graceful-shutdown wait
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private SindriSettings Settings => new(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_ends_every_event_stream_and_serve_exits_promptly()
    {
        var ct = TestContext.Current.CancellationToken;
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null);
        var url = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = url, Timeout = Timeout.InfiniteTimeSpan };
        using var events = await http.GetAsync(new Uri("/sindri/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(ct));
        Assert.StartsWith("event: ", await reader.ReadLineAsync(ct), StringComparison.Ordinal);   // the stream is open

        var token = (await new FileHumanToken(Settings).Read(ct)).Value;   // a stop that ends the process needs the human and its token
        Assert.NotNull(token);
        using var stopRequest = CallerRequests.Post("/sindri/stop", json: null, CallerRequests.Human(token.Value));
        using var stop = await http.SendAsync(stopRequest, ct);
        var rest = await reader.ReadToEndAsync(ct).WaitAsync(_prompt, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.NotNull(rest);
        Assert.Equal(0, code);
    }

    /// <summary>
    /// The order through the real serve. Not the pin: once the stop requests the shutdown, serve's own fallback removal
    /// runs alongside the release and can hide a regression. The pin is the unit test
    /// <see cref="StopProcess.StopProcessTests.StopProcess_removes_the_endpoint_record_before_it_clears_the_grant_and_releases_the_lease"/>.
    /// </summary>
    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_removes_control_json_before_it_releases_the_lease()
    {
        var ct = TestContext.Current.CancellationToken;
        var record = Path.Combine(_home.Path, "control.json");
        bool? recordedAtRelease = null;
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null,
            services => services.AddSingleton<SqliteLease>().AddSingleton<ILease>(sp =>
                new ScriptedLease(sp.GetRequiredService<SqliteLease>()) { BeforeRelease = () => recordedAtRelease = File.Exists(record) }));
        var url = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(10) };

        var token = (await new FileHumanToken(Settings).Read(ct)).Value;   // a stop through the API needs the human and its token
        Assert.NotNull(token);
        using var stopRequest = CallerRequests.Post("/sindri/stop", json: null, CallerRequests.Human(token.Value));
        using var stop = await http.SendAsync(stopRequest, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.Equal(0, code);
        Assert.False(recordedAtRelease);   // why: a client that still finds control.json must not find a process without the lease
        Assert.False(File.Exists(record));
    }

    /// <summary>The endpoint serve recorded once it listens.</summary>
    private async Task<Uri> Endpoint(CancellationToken ct)
    {
        var file = new FileEndpoint(Settings, SliceHarness.Json);
        for (var waited = 0; ; waited += 50)
        {
            if ((await file.Read(ct)).Value is { } recorded)
            {
                return recorded.Url;
            }

            Assert.True(waited < 10_000, "serve didn't record its endpoint within 10 s");
            await Task.Delay(50, ct);
        }
    }
}
