namespace Aw.Sindri.Features.Commands;

/// <summary>The process-wide command gate (API spec §2.1): one command at a time, waited for at most 5 s.</summary>
internal sealed class CommandGateLock : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>How long a command waits for the gate before SND-API-009.</summary>
    internal static TimeSpan Limit { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Waits for the gate for up to <see cref="Limit"/> on <paramref name="clock"/>.</summary>
    /// <returns>True when entered (call <see cref="Exit"/>), false when the limit passed first.</returns>
    internal async ValueTask<bool> Enter(TimeProvider clock, CancellationToken cancellationToken)
    {
        using var limit = new CancellationTokenSource(Limit, clock);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, cancellationToken);
        try
        {
            await _gate.WaitAsync(either.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Leaves the gate.</summary>
    internal void Exit() => _gate.Release();

    public void Dispose() => _gate.Dispose();
}
