namespace Aw.Sindri.Features.Commands;

/// <summary>The process-wide command gate (API spec §2.1): one command at a time, waited for at most 5 s. A stop never waits on it and cancels whatever holds it.</summary>
internal sealed class CommandGateLock : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>Cancelled when the process starts to stop: every gated command runs under it, so a long one ends instead of outliving serve.</summary>
    internal CancellationToken Stopping => _stopping.Token;

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

    /// <summary>Cancels the command holding the gate, and any that would wait for it: the process is stopping.</summary>
    internal void Stop() => _stopping.Cancel();

    public void Dispose()
    {
        _gate.Dispose();
        _stopping.Dispose();
    }
}
