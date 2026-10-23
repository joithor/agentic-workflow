using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P4a acceptance check 1 (engine spec rev 8.1, "Phase 4a"): the dev server starts with 'sindri serve', loopback only,
/// its database under the Sindri home. Runs the real pinned 'temporal' CLI (binaries.lock); without it the test fails and
/// says how to install it, so a missing binary never reads as a pass.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task Serve_starts_the_dev_server_on_loopback_with_its_database_under_the_home()
    {
        var temporal = PinnedTemporal.Path();
        var server = new TemporalDevServer(ChildEnvironment.Start);

        var running = await server.StartAsync(new TemporalServerStart(temporal, _home.Path), TestContext.Current.CancellationToken);
        try
        {
            Assert.True(IPAddress.IsLoopback(running.Frontend.Address), $"frontend {running.Frontend} is not loopback");
            Assert.Equal(Path.Combine(_home.Path, SindriTemporal.DatabaseFile), running.DatabasePath);
            Assert.True(File.Exists(running.DatabasePath), $"no database at {running.DatabasePath}");
            Assert.True(running.ProcessId > 0);
        }
        finally
        {
            await server.StopAsync(running, TestContext.Current.CancellationToken);
        }
    }

    [Fact(Timeout = 60_000)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_start_whose_server_comes_up_seconds_late_waits_for_it_and_is_not_cut_short_by_the_read_bound()
    {
        var ct = TestContext.Current.CancellationToken;
        var slow = Path.Combine(_home.Path, "slow-temporal.sh");
        _ = ExecutableScript.Write(slow, $"sleep 4\nexec \"{PinnedTemporal.Path()}\" \"$@\"\n");   // the pinned CLI, 4 s after the process starts: a cold box
        var server = new TemporalDevServer(ChildEnvironment.Start);

        var running = await server.StartAsync(new TemporalServerStart(slow, _home.Path), ct);
        try
        {
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(running.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
        }
        finally
        {
            await server.StopAsync(running, ct);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_server_left_by_a_serve_that_died_is_adopted_by_the_next_start()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        var first = await new TemporalDevServer(ChildEnvironment.Start).StartAsync(start, ct);   // the serve that started it is gone; its server isn't
        var next = new TemporalDevServer(ChildEnvironment.Start);
        try
        {
            var adopted = await next.StartAsync(start, ct);

            Assert.Equal((first.ProcessId, first.Frontend.Port, true), (adopted.ProcessId, adopted.Frontend.Port, adopted.Adopted));
        }
        finally
        {
            await next.StopAsync(first, ct);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_server_on_the_home_database_that_doesnt_answer_is_replaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        var stuck = await server.StartAsync(start, ct);
        DevServerProcesses.Signal(stuck.ProcessId, "STOP");   // alive, holding the database, answering nothing
        RunningTemporalServer? replacement = null;
        try
        {
            replacement = await server.StartAsync(start, ct);

            Assert.NotEqual(stuck.ProcessId, replacement.ProcessId);
            Assert.False(replacement.Adopted);
            Assert.True(DevServerProcesses.Gone(stuck.ProcessId), "the stuck server still runs");
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(replacement.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
        }
        finally
        {
            DevServerProcesses.Signal(stuck.ProcessId, "KILL");
            if (replacement is not null)
            {
                await server.StopAsync(replacement, ct);
            }
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_restart_on_the_same_database_keeps_the_namespace_and_the_search_attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var start = new TemporalServerStart(PinnedTemporal.Path(), _home.Path);
        await server.StopAsync(await server.StartAsync(start, ct), ct);

        var restarted = await server.StartAsync(start, ct);
        try
        {
            Assert.False(restarted.Adopted);
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(restarted.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
            var listed = await Temporalio.Client.TemporalConnection.CreateLazy(new(restarted.Frontend.ToString())).OperatorService
                .ListSearchAttributesAsync(new() { Namespace = TemporalSettings.DefaultNamespace }, new() { CancellationToken = ct });
            Assert.All(SindriTemporal.SearchAttributes, a => Assert.Equal(a.Type, listed.CustomAttributes[a.Name]));
        }
        finally
        {
            await server.StopAsync(restarted, ct);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task A_picked_port_taken_before_the_server_binds_is_retried_with_new_ports()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rival = Occupy();
        var picks = 0;
        var server = new TemporalDevServer(ChildEnvironment.Start, () => Interlocked.Increment(ref picks) == 1 ? PortOf(rival) : FreePort());   // the first pick (the frontend) loses the race

        var running = await server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct);
        try
        {
            Assert.NotEqual(PortOf(rival), running.Frontend.Port);
            Assert.Equal(DevServerAnswer.Ready, (await DevServerProbe.Ask(running.Frontend, TemporalSettings.DefaultNamespace, TimeSpan.FromSeconds(5), ct)).Answer);
            Assert.Contains("picking new ports", await File.ReadAllTextAsync(Path.Combine(_home.Path, "logs", SindriTemporal.LogFile), ct), StringComparison.Ordinal);
        }
        finally
        {
            await server.StopAsync(running, ct);
        }
    }

    [Fact(Timeout = 90_000)]
    public async Task A_start_whose_ports_are_taken_every_time_gives_up_after_the_bounded_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rival = Occupy();
        var picks = 0;
        var server = new TemporalDevServer(ChildEnvironment.Start, () => Interlocked.Increment(ref picks) % 3 == 1 ? PortOf(rival) : FreePort());   // the frontend pick of every attempt loses

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct));

        Assert.Contains("address already in use", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TemporalDevServer.MaxPortAttempts * 3, picks);
        Assert.Empty(TemporalDevServer.Find(_home.Path));
    }

    private static TcpListener Occupy()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static int FreePort()
    {
        using var listener = Occupy();
        return PortOf(listener);
    }

    [Fact(Timeout = 60_000)]
    public async Task Stop_takes_the_server_down()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new TemporalDevServer(ChildEnvironment.Start);
        var running = await server.StartAsync(new TemporalServerStart(PinnedTemporal.Path(), _home.Path), ct);

        await server.StopAsync(running, ct);

        Assert.True(DevServerProcesses.Gone(running.ProcessId));
        Assert.Empty(TemporalDevServer.Find(_home.Path));
    }

    [Fact]
    public void The_pin_in_code_is_the_pin_in_binaries_lock() => Assert.Equal(SindriTemporal.PinnedVersion, PinnedTemporal.Version);
}
