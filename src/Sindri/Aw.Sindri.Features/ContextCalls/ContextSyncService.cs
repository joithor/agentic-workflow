using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-sync' scheduled service (context spec §8.8, S-5B): syncs the intent sources into the intent layer on the profile's cadence and records
/// one 'context.sources.synced' event per source. It runs outside the command gate, like 'context-rebuild', which is how the stage works before API P6's job route.
/// </summary>
internal sealed class ContextSyncService : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-sync");

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException("S-5B slice 6");
}
