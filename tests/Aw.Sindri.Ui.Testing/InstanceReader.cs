using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The running instance's side in a test: reads <see cref="ISingleInstance.Messages"/> in the background from the moment
/// it is created, as the app's listener does, so a later launch's send is handled (and acknowledged).
/// </summary>
internal sealed class InstanceReader : IAsyncDisposable
{
    private readonly List<string> _seen = [];
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop;
    private readonly Task _reading;

    /// <summary>Starts reading <paramref name="instance"/>'s messages.</summary>
    /// <param name="instance">The claimed instance.</param>
    /// <param name="cancellationToken">The test's token.</param>
    public InstanceReader(ISingleInstance instance, CancellationToken cancellationToken)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _reading = Task.Run(async () =>
        {
            await using var messages = instance.Messages(_stop.Token).GetAsyncEnumerator(_stop.Token);
            var next = messages.MoveNextAsync();
            started.SetResult();
            try
            {
                while (await next)
                {
                    lock (_gate)
                    {
                        _seen.Add(messages.Current.Text);
                    }

                    next = messages.MoveNextAsync();
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // Stopped.
            }
        }, CancellationToken.None);
        started.Task.Wait(cancellationToken);
    }

    /// <summary>The messages read so far, in order.</summary>
    public IReadOnlyList<string> Seen
    {
        get
        {
            lock (_gate)
            {
                return [.. _seen];
            }
        }
    }

    /// <summary>Waits up to 5 s for <paramref name="count"/> messages.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The messages read by then (fewer when they didn't arrive).</returns>
    public async Task<IReadOnlyList<string>> Take(int count)
    {
        for (var i = 0; i < 500 && Seen.Count < count; i++)
        {
            await Task.Delay(10, CancellationToken.None);
        }

        return Seen;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _reading;
        _stop.Dispose();
    }
}
