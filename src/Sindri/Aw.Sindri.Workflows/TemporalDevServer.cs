namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Temporal dev server 'sindri serve' keeps beside itself (engine spec rev 8 §0 "Operations"):
/// 'temporal server start-dev --db-filename &lt;home&gt;/temporal.db --ip 127.0.0.1 --port &lt;p&gt; --ui-port &lt;p&gt;
/// --namespace sindri'. P0 contract stub; engine P0 unit (a) implements it.
/// </summary>
internal sealed class TemporalDevServer
{
    /// <summary>Starts the server, or finds the one already running, and waits until its frontend answers.</summary>
    /// <param name="start">The executable and the Sindri home.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running server.</returns>
    public Task<RunningTemporalServer> StartAsync(TemporalServerStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P0 unit (a): start 'temporal server start-dev' on loopback");

    /// <summary>Stops a server this process started.</summary>
    /// <param name="server">The server.</param>
    /// <param name="cancellationToken">Cancels the wait for its exit.</param>
    /// <returns>A task that completes when the process has exited.</returns>
    public Task StopAsync(RunningTemporalServer server, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P0 unit (a): stop the dev server");
}
