using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.JobProbe;

/// <summary>The job-probe diagnostic service (API P6 unit I); stub, red.</summary>
internal sealed class JobProbeService(IJobContextAccessor jobs, TimeProvider clock) : IScheduledService
{
    public static readonly ServiceName ServiceId = ServiceName.From("job-probe");

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = (jobs, clock, run, cancellationToken);
        throw new NotSupportedException();
    }
}
