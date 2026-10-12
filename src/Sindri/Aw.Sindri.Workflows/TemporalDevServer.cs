namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Temporal dev server 'sindri serve' keeps beside itself (engine spec rev 8 §0 "Operations"):
/// 'temporal server start-dev --db-filename &lt;home&gt;/temporal.db --ip 127.0.0.1 --port &lt;p&gt; --ui-port &lt;p&gt;
/// --namespace sindri', plus the search attributes. It is started through /bin/sh with exec (so the pid is the server's)
/// with stdin from /dev/null and its output appended to &lt;home&gt;/logs/temporal.log under umask 077, so it holds no pipe of
/// serve's and outlives a serve that dies. A start first looks for a server already running on the home's database: one
/// that answers is adopted, one that doesn't is killed and replaced.
/// </summary>
internal sealed class TemporalDevServer
{
    /// <summary>Starts the server, or adopts the one already running on the home's database, and waits until its frontend answers.</summary>
    /// <param name="start">The executable and the Sindri home.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running server.</returns>
    public Task<RunningTemporalServer> StartAsync(TemporalServerStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P0 unit (a): start 'temporal server start-dev' on loopback");

    /// <summary>Stops a server: SIGTERM, then SIGKILL if it hasn't exited within 10 s.</summary>
    /// <param name="server">The server.</param>
    /// <param name="cancellationToken">Cancels the wait for its exit.</param>
    /// <returns>A task that completes when the process has exited.</returns>
    public Task StopAsync(RunningTemporalServer server, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P0 unit (a): stop the dev server");

    /// <summary>The servers running on <paramref name="home"/>'s database.</summary>
    /// <param name="home">The Sindri home.</param>
    /// <returns>The servers.</returns>
    public static IReadOnlyList<RunningTemporalServer> Find(string home) =>
        throw new NotSupportedException("engine P0 unit (a): find the dev server on the home's database");
}
