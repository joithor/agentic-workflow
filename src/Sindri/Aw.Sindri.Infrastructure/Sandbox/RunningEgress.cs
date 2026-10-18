namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One launch's running listeners; disposing stops them, writes the log and removes the socket directory. Disposing twice is a no-op.</summary>
internal sealed class RunningEgress : IAsyncDisposable
{
    private static readonly TimeSpan _flushEvery = TimeSpan.FromSeconds(1);
    private readonly EgressListener _proxy;
    private readonly EgressListener _sindri;
    private readonly CancellationTokenSource _stopFlushing = new();
    private readonly Task _flushing;
    private int _disposed;

    internal RunningEgress(string socat, string directory, EgressLog log, EgressListener proxy, EgressListener sindri)
    {
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
            try
            {
                await _proxy.DisposeAsync().ConfigureAwait(false);
                await _sindri.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Log.Flush();
                }
                finally
                {
                    _stopFlushing.Dispose();
                    if (System.IO.Directory.Exists(Directory))
                    {
                        System.IO.Directory.Delete(Directory, recursive: true);
                    }
                }
            }
        }
    }

    private async Task FlushLoop()
    {
        using var timer = new PeriodicTimer(_flushEvery);
        try
        {
            while (await timer.WaitForNextTickAsync(_stopFlushing.Token).ConfigureAwait(false))
            {
                Log.Flush();
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose ends the loop and writes the last state.
        }
        catch (IOException)
        {
            // A full disk shows up again at dispose; the loop must not take the run down.
        }
    }
}
