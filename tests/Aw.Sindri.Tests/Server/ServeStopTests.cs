using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>'sindri serve' as the CLI runs it, stopped through the API while a client follows the event stream.</summary>
public sealed class ServeStopTests : IDisposable
{
    private static readonly TimeSpan _prompt = TimeSpan.FromSeconds(5);   // well under Kestrel's 30 s graceful-shutdown wait
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_ends_every_event_stream_and_serve_exits_promptly()
    {
        var ct = TestContext.Current.CancellationToken;
        var temporal = new FakeTemporalServer();
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null,
            services => NoTemporal.Use(services, temporal));
        var recorded = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = recorded.Url, Timeout = Timeout.InfiniteTimeSpan };
        using var events = await http.GetAsync(new Uri("/sindri/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(ct));
        Assert.StartsWith("event: ", await reader.ReadLineAsync(ct), StringComparison.Ordinal);   // the stream is open

        using var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, ct);
        var rest = await reader.ReadToEndAsync(ct).WaitAsync(_prompt, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.NotNull(rest);
        Assert.Equal(0, code);
        Assert.NotNull(recorded.Temporal);
        Assert.Equal((1, 1), (temporal.Starts, temporal.Stops));   // why: control.json named the server serve started, and the stop took it down
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
            services =>
            {
                NoTemporal.Use(services);
                _ = services.AddSingleton<SqliteLease>().AddSingleton<ILease>(sp =>
                    new ScriptedLease(sp.GetRequiredService<SqliteLease>()) { BeforeRelease = () => recordedAtRelease = File.Exists(record) });
            });
        var recorded = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = recorded.Url, Timeout = TimeSpan.FromSeconds(10) };

        using var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.Equal(0, code);
        Assert.False(recordedAtRelease);   // why: a client that still finds control.json must not find a process without the lease
        Assert.False(File.Exists(record));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_stop_waits_for_the_worker_before_it_stops_the_dev_server()
    {
        var ct = TestContext.Current.CancellationToken;
        var temporal = new FakeTemporalServer();
        var worker = new ScriptedWorker { Drain = TimeSpan.FromMilliseconds(200) };
        bool? workerStoppedAtServerStop = null;
        temporal.BeforeStop = () => workerStoppedAtServerStop = worker.Stopped;
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null,
            services => NoTemporal.Use(services, temporal, worker));
        var recorded = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = recorded.Url, Timeout = TimeSpan.FromSeconds(10) };

        using var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.Equal(0, code);
        Assert.True(workerStoppedAtServerStop);   // why: serve tracks its worker, so the stop waits for it before the server goes
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_worker_that_faults_on_its_way_out_still_lets_serve_stop_cleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var temporal = new FakeTemporalServer();
        var worker = new ScriptedWorker { Fault = new InvalidOperationException("poller failed") };
        var serve = ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, TextWriter.Null,
            services => NoTemporal.Use(services, temporal, worker));
        var recorded = await Endpoint(ct);
        using var http = new HttpClient { BaseAddress = recorded.Url, Timeout = TimeSpan.FromSeconds(10) };

        using var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, ct);
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.True(stop.IsSuccessStatusCode);
        Assert.Equal(0, code);   // why: a worker fault is the worker's to log; it must not throw past serve's stop
        Assert.Null(temporal.Running);
        Assert.False(File.Exists(Path.Combine(_home.Path, "control.json")));
    }

    /// <summary>The endpoint serve recorded once it listens.</summary>
    private async Task<ControlEndpoint> Endpoint(CancellationToken ct)
    {
        var file = new FileEndpoint(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml")), SliceHarness.Json);
        for (var waited = 0; ; waited += 50)
        {
            if ((await file.Read(ct)).Value is { } recorded)
            {
                return recorded;
            }

            Assert.True(waited < 10_000, "serve didn't record its endpoint within 10 s");
            await Task.Delay(50, ct);
        }
    }
}
