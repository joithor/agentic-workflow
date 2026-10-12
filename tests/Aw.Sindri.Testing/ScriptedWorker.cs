using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>
/// A worker that polls nothing and, as the port says, completes once it is stopped: after <see cref="Drain"/>, or with
/// <see cref="Fault"/> thrown when set. The fault is the one deliberate breach of the port's contract (Run never
/// throws), for serve's guard; without it the worker passes TemporalWorkerContract.
/// </summary>
public sealed class ScriptedWorker : ITemporalWorker
{
    /// <summary>How long the worker takes to stop after it is cancelled (the SDK drains its pollers).</summary>
    public TimeSpan Drain { get; init; } = TimeSpan.Zero;

    /// <summary>When set, the worker stops by throwing it.</summary>
    public Exception? Fault { get; init; }

    /// <summary>Whether the worker has stopped.</summary>
    public bool Stopped { get; private set; }

    /// <inheritdoc/>
    public async Task Run(TemporalEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // stopped
        }

        await Task.Delay(Drain, TimeProvider.System, CancellationToken.None);
        Stopped = true;
        if (Fault is { } fault)
        {
            throw fault;
        }
    }
}
