using System.Collections.Concurrent;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// Runs a test body the way the app's UI thread runs view-model code: on one thread with a single-threaded
/// synchronization context, so every continuation runs on that thread, one at a
/// time, and a completion from another read cannot interleave with the code awaiting it.
/// </summary>
public static class UiThread
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(30);

    /// <summary>Runs <paramref name="body"/> on its own pumped thread; a deadlock fails after 30 seconds instead of hanging.</summary>
    public static async Task Run(Func<Task> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_bound);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Pump(body, done, limit.Token)) { IsBackground = true, Name = "ui-thread" };
        thread.Start();
        await done.Task.ConfigureAwait(true);
    }

    private static void Pump(Func<Task> body, TaskCompletionSource done, CancellationToken token)
    {
        var context = new PumpContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = body();
            _ = task.ContinueWith(static (_, state) => ((PumpContext)state!).Complete(), context, TaskScheduler.Default);   // why: the state is the PumpContext passed on the line above
            context.Drain(token);
            _ = !task.IsCompleted || task.IsCanceled
                ? done.TrySetCanceled(token)
                : task.Exception is { } failure ? done.TrySetException(failure.InnerExceptions) : done.TrySetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                _ = _queue.TryAdd((d, state));
            }
            catch (InvalidOperationException)
            {
                // The run has ended (CompleteAdding): work posted after it, such as a timer's finalizer, is dropped.
                // why: a throw here can land on the finalizer thread and kill the test host
            }
        }

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException("The UI thread stand-in takes posted work only.");

        public void Complete() => _queue.CompleteAdding();

        public void Drain(CancellationToken token)
        {
            try
            {
                foreach (var (callback, state) in _queue.GetConsumingEnumerable(token))
                {
                    callback(state);
                }
            }
            catch (OperationCanceledException)
            {
                // The bound ran out: Pump reports the task as not completed.
            }
        }
    }
}
