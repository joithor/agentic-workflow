using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'stuck' whose runs wait until the test releases them (S1-D10), counting how many began.</summary>
internal sealed class BlockedService : IScheduledService
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    public static ServiceName Name { get; } = ServiceName.From("stuck");

    public int Started => Volatile.Read(ref _started);

    public ServiceName Identity() => Name;

    public void Release() => _release.TrySetResult();

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _started);
        await _release.Task.WaitAsync(cancellationToken);
        return ScheduledResult.Worked;
    }
}
