namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One launch's running listeners; disposing stops them, writes the log and removes the socket directory. Disposing twice is a no-op.</summary>
internal sealed class RunningEgress : IAsyncDisposable
{
    private static readonly TimeSpan _flushEvery = TimeSpan.FromSeconds(1);
    private readonly EgressListener _proxy;
    private readonly EgressListener _sindri;
    private readonly CancellationTokenSource _stopFlushing = new();
    private readonly Task _flushing;
    private readonly Action<string> _warn;
    private int _disposed;

    internal RunningEgress(string socat, string directory, EgressLog log, EgressListener proxy, EgressListener sindri, Action<string> warn)
    {
        _warn = warn;
        Socat = socat;
        Directory = directory;
        Log = log;
        _proxy = proxy;
        _sindri = sindri;
        _flushing = Task.Run(FlushLoop, _stopFlushing.Token);
    }

    /// <summary>The socat to put in the init script.</summary>
    public string Socat { get; }

    /// <summary>The egress log.</summary>
    public EgressLog Log { get; }

    /// <summary>The short socket directory.</summary>
    public string Directory { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            await _stopFlushing.CancelAsync().ConfigureAwait(false);
            await _flushing.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The loop never started before dispose; there is nothing to wait for.
        }
        finally
        {
            await Cleanup().ConfigureAwait(false);
        }
    }

    private async Task Cleanup()
    {
        // Every step runs and none throws: a cleanup problem must not replace the agent's real outcome.
        await Safely(() => _proxy.DisposeAsync().AsTask(), "stopping the proxy listener").ConfigureAwait(false);
        await Safely(() => _sindri.DisposeAsync().AsTask(), "stopping the Sindri listener").ConfigureAwait(false);
        await Safely(() =>
        {
            Log.Flush();
            return Task.CompletedTask;
        }, "writing egress.log").ConfigureAwait(false);
        _stopFlushing.Dispose();
        await Safely(() =>
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }

            return Task.CompletedTask;
        }, "removing the socket directory").ConfigureAwait(false);
    }

    private async Task Safely(Func<Task> step, string what)
    {
        try
        {
            await step().ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ObjectDisposedException or OperationCanceledException)
        {
            _warn("egress: " + what + " failed: " + e.Message);
        }
    }

    private async Task FlushLoop()
    {
        using var timer = new PeriodicTimer(_flushEvery);
        try
        {
            while (await timer.WaitForNextTickAsync(_stopFlushing.Token).ConfigureAwait(false))
            {
                try
                {
                    Log.Flush();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    _warn("egress: writing egress.log failed, retrying next tick: " + e.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose ends the loop and writes the last state.
        }
    }
}
