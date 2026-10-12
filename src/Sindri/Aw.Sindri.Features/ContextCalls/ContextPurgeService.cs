using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The 'context-purge' service (context spec §6.2): deletes expired map exports and retained map versions past the newest 20.</summary>
internal sealed class ContextPurgeService : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-purge");

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
