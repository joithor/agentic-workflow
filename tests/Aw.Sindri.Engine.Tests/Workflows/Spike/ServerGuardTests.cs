using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// A test host that is SIGKILLed never runs Dispose, so the Temporal server it started used to be left running (63 of them
/// took the box down on 2026-10-14). The guard ties the server to the host: when the host is gone the server is stopped.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ServerGuardTests
{
    /// <summary>
    /// How long a dev server may take to create its database. A cold first start (the binary not in the page cache, a gate box
    /// building and testing many projects at once) took over the old 30 s at position 1 of a merge train (gate 826); the wait
    /// returns the moment the file exists, so a generous bound costs nothing when the box is quiet.
    /// </summary>
    private static readonly TimeSpan _startupLimit = TimeSpan.FromSeconds(120);

    /// <summary>
    /// A stand-in host (a shell) starts a real dev server as its child, the way the SDK does, and is then SIGKILLed.
    /// The server is found by the pid the host prints (echo $!), never by a global count: other lanes run servers too.
    /// Its database file existing means startup is done.
    /// </summary>
    [Fact(Timeout = 240_000)]
    public Task A_server_started_by_a_host_that_is_SIGKILLed_dies_within_ten_seconds() => ServerOutlivingItsHost(guarded: true, kill: true, TestContext.Current.CancellationToken);

    /// <summary>The same for a host that ends normally without stopping the server (a test that forgot, or a Dispose that failed).</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 240_000)]
    public Task A_server_left_running_by_a_host_that_exits_normally_dies_within_ten_seconds() => ServerOutlivingItsHost(guarded: true, kill: false, TestContext.Current.CancellationToken);

    /// <summary>Negative control: without the guard the same SIGKILLed host leaves its server running, so the tests above prove the guard and not the shell.</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 240_000)]
    public async Task Without_the_guard_the_server_outlives_a_SIGKILLed_host()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempHome();
        var (host, server) = await StartHost(Path.Combine(temp.Path, "guard-test.db"), guarded: false, wait: true, ct);
        using var _ = host;
        try
        {
            host.Kill();
            await Task.Delay(TimeSpan.FromSeconds(3), ct);

            Assert.True(Alive(server), "the unguarded server died with its host: the guard tests prove nothing");
        }
        finally
        {
            if (Alive(server))
            {
                Process.GetProcessById(server).Kill();   // this test's own server
            }
        }
    }

    /// <summary>Parallel test classes ask for the launcher at once; it is written once, complete and executable, and never rewritten under an exec.</summary>
    /// <returns>The test.</returns>
    [Fact(Timeout = 240_000)]
    public async Task Parallel_requests_for_the_launcher_share_one_complete_script_that_is_not_rewritten()
    {
        var ct = TestContext.Current.CancellationToken;
        var executable = PinnedTemporal.Unwrapped();
        var owner = Environment.ProcessId;

        var paths = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => ServerGuard.Wrap(executable, owner), ct)));
        var written = File.GetLastWriteTimeUtc(paths[0]);
        await Task.Delay(50, ct);
        var again = ServerGuard.Wrap(executable, owner);

        _ = Assert.Single(paths.Distinct(StringComparer.Ordinal));
        Assert.Equal(paths[0], again);
        Assert.Equal(written, File.GetLastWriteTimeUtc(again));
        Assert.StartsWith("#!/bin/sh\n", await File.ReadAllTextAsync(again, ct), StringComparison.Ordinal);
        Assert.True(File.GetUnixFileMode(again).HasFlag(UnixFileMode.UserExecute));
    }

    private static async Task ServerOutlivingItsHost(bool guarded, bool kill, CancellationToken ct)
    {
        using var temp = new TempHome();
        var (host, server) = await StartHost(Path.Combine(temp.Path, "guard-test.db"), guarded, wait: kill, ct);
        using var _ = host;
        try
        {
            if (kill)
            {
                host.Kill();   // SIGKILL on Unix: no Dispose, no finalizer, no shutdown hook runs in the host
            }
            else
            {
                await host.StandardInput.WriteLineAsync("go".AsMemory(), ct);   // the host ends normally, now that the server is up
                await host.WaitForExitAsync(ct);
            }

            await WaitFor(() => !Alive(server), TimeSpan.FromSeconds(10), string.Create(CultureInfo.InvariantCulture, $"server {server} outlived its host"), ct);
        }
        finally
        {
            if (Alive(server))
            {
                Process.GetProcessById(server).Kill();   // this test's own server; leave nothing behind even when red
            }
        }
    }

    /// <summary>Starts a shell as the stand-in host; it starts a dev server as its child and prints the server's pid. A host that isn't <paramref name="wait"/>ing exits when it reads a line from stdin.</summary>
    private static async Task<(Process Host, int Server)> StartHost(string database, bool guarded, bool wait, CancellationToken ct)
    {
        // the host reads the server path from stdin, so the guard can name the host's own pid as the owner
        var host = Process.Start(new ProcessStartInfo("/bin/sh", ["-c",
            "read -r server; \"$server\" server start-dev --headless --db-filename \"$0\" --port \"$1\" --log-level error </dev/null >/dev/null 2>&1 & echo $!; if [ \"$2\" = wait ]; then wait; else read -r _; fi",
            database, FreePort(), wait ? "wait" : "exit"])
        { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true })!;   // why: Start returns a process for a shell that exists
        var server = 0;
        try
        {
            var path = guarded ? ServerGuard.Wrap(PinnedTemporal.Unwrapped(), host.Id) : PinnedTemporal.Unwrapped();
            await host.StandardInput.WriteLineAsync(path.AsMemory(), ct);
            await host.StandardInput.FlushAsync(ct);
            server = int.Parse((await host.StandardOutput.ReadLineAsync(ct))!, CultureInfo.InvariantCulture);
            await WaitFor(() => File.Exists(database), _startupLimit, "the server never created its database", ct);
            Assert.True(Alive(server), "the server did not start");
            return (host, server);
        }
        catch
        {
            host.Kill();
            host.Dispose();
            if (server > 0 && Alive(server))
            {
                Process.GetProcessById(server).Kill();   // an unguarded server would otherwise be orphaned for good
            }

            throw;
        }
    }

    private static bool Alive(int pid)
    {
        // a killed child of a killed host lingers as a zombie until init reaps it; that is dead
        using var ps = Process.Start(new ProcessStartInfo("/bin/ps", ["-o", "stat=", "-p", pid.ToString(CultureInfo.InvariantCulture)])
        { UseShellExecute = false, RedirectStandardOutput = true })!;   // why: Start returns a process for ps
        var state = ps.StandardOutput.ReadToEnd().Trim();
        ps.WaitForExit();
        return state.Length > 0 && state[0] != 'Z';
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan limit, string failure, CancellationToken ct)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > limit)
            {
                Assert.Fail(failure);
            }

            await Task.Delay(100, ct);
        }
    }

    private static string FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture);
    }
}
