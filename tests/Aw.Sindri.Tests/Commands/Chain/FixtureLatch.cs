namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>What the fixture handler saw: how many runs started, and the latch a 'block' run waits on.</summary>
internal sealed class FixtureLatch
{
    private int _started;

    public int Started => Volatile.Read(ref _started);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Start() => Interlocked.Increment(ref _started);
}
