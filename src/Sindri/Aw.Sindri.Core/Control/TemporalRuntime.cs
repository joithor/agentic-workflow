using Aw.Kernel;

namespace Aw.Sindri.Core.Control;

/// <summary>
/// The Temporal side 'sindri serve' keeps (engine spec §0 "Operations"): the worker it runs and the dev server it started
/// or adopted. <see cref="Stop"/> takes them down in that order, so the server never goes while the worker still polls it.
/// </summary>
/// <param name="server">The dev server.</param>
/// <param name="clock">The clock the worker wait runs on.</param>
public sealed class TemporalRuntime(ITemporalServer server, TimeProvider clock)
{
    private Task _worker = Task.CompletedTask;

    /// <summary>How long <see cref="Stop"/> waits for the worker before it stops the server anyway.</summary>
    public static TimeSpan WorkerStopWait { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Records the worker serve runs; it stops once the shutdown is requested.</summary>
    /// <param name="worker">The worker's run, which completes once it has stopped.</param>
    public void Track(Task worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        Volatile.Write(ref _worker, worker);
    }

    /// <summary>
    /// Waits for the tracked worker to stop (at most <see cref="WorkerStopWait"/>), then stops the dev server. Call it
    /// after the shutdown is requested: that is what stops the worker.
    /// </summary>
    /// <param name="cancellationToken">Cancels the waits.</param>
    /// <returns>Success, or why the server couldn't be stopped.</returns>
    public async ValueTask<Result<Unit>> Stop(CancellationToken cancellationToken)
    {
        using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            // WhenAny never throws: a worker that faulted has stopped too, and serve reports its fault when it awaits it
            _ = await Task.WhenAny(Volatile.Read(ref _worker), Task.Delay(WorkerStopWait, clock, wait.Token)).ConfigureAwait(false);
            await wait.CancelAsync().ConfigureAwait(false);   // drops the timer
        }

        return await server.Stop(cancellationToken).ConfigureAwait(false);
    }
}
