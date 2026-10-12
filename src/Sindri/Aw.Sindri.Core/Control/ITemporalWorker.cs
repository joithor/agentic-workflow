namespace Aw.Sindri.Core.Control;

/// <summary>
/// The Temporal worker 'sindri serve' runs beside the control plane (port; engine spec rev 8 §0 "Operations"): it polls
/// task queue 'sindri' on the dev server serve keeps, so instances move while serve runs. Engine P4a.
/// </summary>
public interface ITemporalWorker
{
    /// <summary>Polls the dev server at <paramref name="endpoint"/> until <paramref name="cancellationToken"/> is cancelled.</summary>
    /// <param name="endpoint">The dev server serve started or adopted.</param>
    /// <param name="cancellationToken">Stops the worker.</param>
    /// <returns>
    /// Completes once the worker has stopped, and never faults: a frontend that doesn't answer or a worker that fails is
    /// logged, so <see cref="TemporalRuntime"/>'s stop can rely on it (contract: TemporalWorkerContract).
    /// </returns>
    public Task Run(TemporalEndpoint endpoint, CancellationToken cancellationToken);
}
