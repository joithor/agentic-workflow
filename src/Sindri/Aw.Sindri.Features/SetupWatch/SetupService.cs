using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The 'setup' built-in service (setup-once spec §5): it runs without a profile entry, so the services list shows it, the scheduler runs it as 'service:setup' and PauseService refuses it (SND-SVC-404, as for
/// every service the approved profile doesn't name). This is its shell: woken by events and a 60-second backstop, it has no step to work on yet, so a tick is quiet. The steps (SO-A3, SO-A4, SO-A6) fill it in.
/// </summary>
internal sealed class SetupService : IScheduledService
{
    /// <summary>The service's name in the schedule ('setup'); its actor is 'service:setup'.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("setup");

    /// <summary>The backstop tick without a profile entry: every 60 seconds.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(60)));

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ScheduledResult>>(ScheduledResult.Quiet);
}
