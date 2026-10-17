namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>What the fixture handler saw: how many runs started, and the latch a 'block' run waits on.</summary>
internal sealed class FixtureLatch
{
    private int _started;

    public int Started => Volatile.Read(ref _started);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the first run is inside the handler.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when a 'stubborn' or 'own-cancel' work has finished waiting and is leaving its handler.</summary>
    public TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Start()
    {
        _ = Interlocked.Increment(ref _started);
        _ = Entered.TrySetResult();
    }
}
