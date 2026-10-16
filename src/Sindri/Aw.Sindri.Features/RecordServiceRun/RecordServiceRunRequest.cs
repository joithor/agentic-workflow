using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RecordServiceRun;

/// <summary>Record one scheduled run as 'service.ran' (API spec C8): the scheduler calls it after every run, whatever its outcome.</summary>
/// <param name="Service">The service that ran (or that this build doesn't have).</param>
/// <param name="Run">The scheduled run (grant and slot).</param>
/// <param name="Outcome">The run's result: success, or the error it failed with.</param>
/// <param name="Job">The job of a manual run (RunService, API P6): its run is keyed by the job, and its slot is when the job started. Null for a scheduled run.</param>
public sealed record RecordServiceRunRequest(ServiceName Service, ScheduledRun Run, Result<Unit> Outcome, JobId? Job = null);
