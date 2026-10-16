namespace Aw.Sindri.Host.Commands;

/// <summary>The two Ctrl-C of a followed job as tasks to wait on: the first asks the job to cancel, the second leaves at once.</summary>
internal sealed class JobInterrupts : IDisposable
{
    private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _second = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _firstRegistration;
    private readonly CancellationTokenRegistration _secondRegistration;

    internal JobInterrupts(CancellationToken first, CancellationToken second)
    {
        First = first;
        Second = second;
        _firstRegistration = first.Register(() => _first.TrySetResult());
        _secondRegistration = second.Register(() => _second.TrySetResult());
    }

    /// <summary>The first Ctrl-C.</summary>
    public CancellationToken First { get; }

    /// <summary>The second Ctrl-C.</summary>
    public CancellationToken Second { get; }

    /// <summary>Completes at the first Ctrl-C.</summary>
    public Task FirstSignal => _first.Task;

    /// <summary>Completes at the second Ctrl-C.</summary>
    public Task SecondSignal => _second.Task;

    public void Dispose()
    {
        _firstRegistration.Dispose();
        _secondRegistration.Dispose();
    }
}
