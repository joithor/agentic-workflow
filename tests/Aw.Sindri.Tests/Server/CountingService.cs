using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'counted' that counts its runs, for the built-in schedule (S1-D8).</summary>
internal sealed class CountingService : IScheduledService
{
    private int _runs;

    public static ServiceName Name { get; } = ServiceName.From("counted");

    public int Runs => Volatile.Read(ref _runs);

    public ServiceName Identity() => Name;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _runs);
        return ValueTask.FromResult<Result<ScheduledResult>>(ScheduledResult.Worked);
    }
}
