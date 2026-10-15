using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-rebuild' background service (context spec §6.2, README X63): runs only when the profile names it, on its cadence.
/// It proposes the map from the repo into a draft (extraction takes the box-wide heavy-job lock: SND-CONTEXT-040 when it stays
/// busy) and records 'context.map.built'. Nothing runs map build inside the command gate: until API P6 adds the build job,
/// this service is how a draft appears without the proposals route.
/// </summary>
internal sealed class ContextRebuildService : IScheduledService
{
    /// <summary>The service's name in the profile.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-rebuild");

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException();
}
