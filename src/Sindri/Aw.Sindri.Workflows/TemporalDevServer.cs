using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Temporal dev server 'sindri serve' keeps beside itself (engine spec rev 8 §0 "Operations"):
/// 'temporal server start-dev --db-filename &lt;home&gt;/temporal.db --ip 127.0.0.1 --port &lt;p&gt; --ui-port &lt;p&gt;
/// --namespace &lt;the settings' namespace&gt;', plus the search attributes. It is launched detached: /bin/sh starts it in the background in its
/// own session (perl's POSIX::setsid, as ServeLauncher does) with stdin from /dev/null and its output appended to
/// &lt;home&gt;/logs/temporal.log under umask 077, prints its pid and exits. So the server is never serve's child: it holds no
/// pipe of serve's, a terminal's Ctrl-C doesn't reach it, it outlives a serve that dies, and serve gets no SIGCHLD from it
/// (a stopped child made the runtime's child reaping spin and hang every later Process wait). A start first looks for a
/// server already running on the home's database: one that answers is adopted, one that doesn't is killed and replaced.
/// </summary>
internal sealed class TemporalDevServer
{
    private readonly TimeSpan _adoptWait = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _readyWait = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _stopWait = TimeSpan.FromSeconds(10);
    internal const int MaxPortAttempts = 3;

    public TemporalDevServer()
    {
    }

    internal TemporalDevServer(Func<int> pickPort)
    {
        ArgumentNullException.ThrowIfNull(pickPort);
    }

    /// <summary>Starts the server, or adopts the one already running on the home's database, and waits until its frontend answers.</summary>
    /// <param name="start">The executable and the Sindri home.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running server.</returns>
    public async Task<RunningTemporalServer> StartAsync(TemporalServerStart start, CancellationToken cancellationToken)
    {
        var home = Path.GetFullPath(start.SindriHome);
        var database = Path.Combine(home, SindriTemporal.DatabaseFile);
        foreach (var found in DevServerProcesses.On(database))
        {
            if ((await DevServerProbe.Ask(found.Frontend, start.Namespace, _adoptWait, cancellationToken).ConfigureAwait(false)).Answer == DevServerAnswer.Ready)
            {
                await DevServerProbe.RegisterSearchAttributes(found.Frontend, start.Namespace, cancellationToken).ConfigureAwait(false);
                return found with { Adopted = true };
            }

            await Kill(found.ProcessId).ConfigureAwait(false);   // ours by its database, but it doesn't answer: replace it
        }

        return await Launch(start, home, database, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops a server: SIGTERM, then SIGKILL if it hasn't exited within 10 s. A cancelled stop doesn't leave it running:
    /// the server is detached (setsid), so it would outlive serve and its caller; cancelling cuts the SIGTERM grace short
    /// and kills it at once, and only then does the cancellation propagate.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="cancellationToken">Cuts the wait for a clean exit short.</param>
    /// <returns>A task that completes when the process has exited.</returns>
    public async Task StopAsync(RunningTemporalServer server, CancellationToken cancellationToken)
    {
        if (DevServerProcesses.Gone(server.ProcessId))
        {
            return;
        }

        DevServerProcesses.Signal(server.ProcessId, "TERM");
        bool exited;
        try
        {
            exited = await Exited(server.ProcessId, _stopWait, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await Kill(server.ProcessId).ConfigureAwait(false);
            throw;
        }

        if (!exited)
        {
            await Kill(server.ProcessId).ConfigureAwait(false);
        }
    }

    /// <summary>The servers running on <paramref name="home"/>'s database.</summary>
    /// <param name="home">The Sindri home.</param>
    /// <returns>The servers.</returns>
    public static IReadOnlyList<RunningTemporalServer> Find(string home) =>
        DevServerProcesses.On(Path.Combine(Path.GetFullPath(home), SindriTemporal.DatabaseFile));

    private async Task<RunningTemporalServer> Launch(TemporalServerStart start, string home, string database, CancellationToken cancellationToken)
    {
        var logs = Path.Combine(home, "logs");
        if (!Directory.Exists(logs))
        {
            _ = Directory.CreateDirectory(logs);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(logs, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        var log = Path.Combine(logs, SindriTemporal.LogFile);
        var frontend = new IPEndPoint(IPAddress.Loopback, FreePort());
        var ui = FreePort();
        var pid = Detach(start.Executable, ServerArguments(database, frontend.Port, ui, start.Namespace), log);
        var server = new RunningTemporalServer(pid, frontend, database) { UiPort = ui, Executable = start.Executable };
        var waited = Stopwatch.StartNew();
        while (true)
        {
            if (DevServerProcesses.Gone(pid))
            {
                throw new InvalidOperationException($"'temporal server start-dev' exited: {LastLine(log)}");
            }

            var (answer, problem) = await DevServerProbe.Ask(frontend, start.Namespace, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (answer == DevServerAnswer.Ready)
            {
                await DevServerProbe.RegisterSearchAttributes(frontend, start.Namespace, cancellationToken).ConfigureAwait(false);
                return server;
            }

            if (waited.Elapsed > _readyWait)
            {
                await Kill(pid).ConfigureAwait(false);
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"the dev server on {frontend} didn't answer within {_readyWait.TotalSeconds:0} s: {problem}"));
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>'temporal server start-dev' with the database, the ports, the namespace and the search attributes.</summary>
    private static string[] ServerArguments(string database, int port, int ui, string @namespace) =>
    [
        "server", "start-dev", "--db-filename", database, "--ip", "127.0.0.1", "--port", Text(port), "--ui-port", Text(ui),
        "--namespace", @namespace, "--log-level", "warn",
        .. SindriTemporal.SearchAttributes.SelectMany(a => new[] { "--search-attribute", $"{a.Name}={a.Type}" }),
    ];

    /// <summary>Starts the server detached (see the class summary) and returns its pid, which /bin/sh prints.</summary>
    private static int Detach(string executable, string[] server, string log)
    {
        const string Script = "umask 077; log=$1; shift; "
            + "if [ -x /usr/bin/perl ]; then /usr/bin/perl -MPOSIX -e 'POSIX::setsid(); exec @ARGV or die \"exec $ARGV[0]: $!\\n\"' \"$@\" </dev/null >>\"$log\" 2>&1 & "
            + "else \"$@\" </dev/null >>\"$log\" 2>&1 & fi; echo $!";
        string[] arguments = [.. new[] { "-c", Script, "sh", log, executable }, .. server];
        var printed = DevServerProcesses.Capture("/bin/sh", arguments).Trim();
        return int.TryParse(printed, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            ? pid
            : throw new InvalidOperationException($"/bin/sh didn't print the dev server's pid (it printed '{printed}')");
    }

    /// <summary>SIGKILL, then waits (at most 10 s, not cancellable: the signal is sent and the exit follows at once) for the exit.</summary>
    private async Task Kill(int pid)
    {
        DevServerProcesses.Signal(pid, "KILL");
        _ = await Exited(pid, _stopWait, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Waits up to <paramref name="wait"/> for <paramref name="pid"/> to exit (it is never this process's child, so it is polled).</summary>
    private static async Task<bool> Exited(int pid, TimeSpan wait, CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        while (!DevServerProcesses.Gone(pid))
        {
            if (waited.Elapsed > wait)
            {
                return false;
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string LastLine(string log) =>
        File.Exists(log) ? File.ReadLines(log).LastOrDefault(l => l.Length > 0) ?? "no output" : "no log";

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
