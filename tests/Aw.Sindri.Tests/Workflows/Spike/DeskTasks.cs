using System.Collections.Concurrent;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// The desk tasks an async-completion activity recorded: the activity id (the run token) to its task token. Stands in
/// for the 'step.started' ledger row the API reads to map a run token to its task token (engine spec rev 8 §5.3).
/// </summary>
internal sealed class DeskTasks
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _tasks = new(StringComparer.Ordinal);
    private int _opened;

    /// <summary>How many times an activity recorded a task (a re-launch would count twice).</summary>
    public int Opened => Volatile.Read(ref _opened);

    /// <summary>The key of an instance's run: '{address}#{run token}'.</summary>
    /// <param name="address">The workflow id.</param>
    /// <param name="token">The run token.</param>
    /// <returns>The key.</returns>
    public static string Key(string address, string token) => address + "#" + token;

    /// <summary>Records <paramref name="token"/>'s task token.</summary>
    /// <param name="token">The run token (activity id).</param>
    /// <param name="taskToken">The activity's task token.</param>
    public void Open(string token, byte[] taskToken)
    {
        _ = Interlocked.Increment(ref _opened);
        _ = Slot(token).TrySetResult(taskToken);
    }

    /// <summary>Waits until <paramref name="token"/>'s task is recorded.</summary>
    /// <param name="token">The run token.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="within">How long to wait (20 s by default) before a TimeoutException.</param>
    /// <returns>Its task token.</returns>
    public Task<byte[]> WaitFor(string token, CancellationToken cancellationToken, TimeSpan? within = null) =>
        Slot(token).Task.WaitAsync(within ?? TimeSpan.FromSeconds(20), cancellationToken);

    private TaskCompletionSource<byte[]> Slot(string token) =>
        _tasks.GetOrAdd(token, static _ => new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously));
}
