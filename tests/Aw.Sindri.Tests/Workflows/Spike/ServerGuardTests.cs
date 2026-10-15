using Aw.Kernel.Testing;
using System.Diagnostics;
using System.Globalization;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// A test host that is SIGKILLed never runs Dispose, so the Temporal server it started used to be left running (63 of them
/// took the box down on 2026-10-14). The guard ties the server to the host: when the host is gone the server is stopped.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ServerGuardTests
{
    /// <summary>
    /// A stand-in host (a shell) starts a real dev server as its child, the way the SDK does, and is then SIGKILLed.
    /// The server is found by its unique database file, never by a global count: other lanes run servers too.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task A_server_started_by_a_host_that_is_SIGKILLed_dies_within_ten_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempHome();
        var database = Path.Combine(temp.Path, "guard-test.db");

        // the host reads the server path from stdin, so the guard can name the host's own pid as the owner
        using var host = Process.Start(new ProcessStartInfo("/bin/sh", ["-c",
            "read -r server; \"$server\" server start-dev --headless --db-filename \"$0\" --port \"$1\" --log-level error </dev/null >/dev/null 2>&1 & echo $!; wait",
            database, FreePort()])
        { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true })!;   // why: Start returns a process for a shell that exists
        int server;
        try
        {
            await host.StandardInput.WriteLineAsync(ServerGuard.Wrap(PinnedTemporal.Path(), host.Id).AsMemory(), ct);
            host.StandardInput.Close();
            server = int.Parse((await host.StandardOutput.ReadLineAsync(ct))!, CultureInfo.InvariantCulture);
            await WaitFor(() => File.Exists(database), TimeSpan.FromSeconds(20), "the server never created its database", ct);
            Assert.True(Alive(server), "the server did not start");
        }
        catch
        {
            host.Kill();
            throw;
        }

        try
        {
            host.Kill();   // SIGKILL on Unix: no Dispose, no finalizer, no shutdown hook runs in the host
            await WaitFor(() => !Alive(server), TimeSpan.FromSeconds(10), string.Create(CultureInfo.InvariantCulture, $"server {server} outlived its SIGKILLed host"), ct);
        }
        finally
        {
            if (Alive(server))
            {
                Process.GetProcessById(server).Kill();   // this test's own server; leave nothing behind even when red
            }
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
