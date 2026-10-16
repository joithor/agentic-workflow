using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>Appends a service's 'service.ran' (API spec C8): the scheduler's records through the RecordServiceRun command, a manual run (RunService, API P6) through this port.</summary>
public interface IServiceRunRecorder
{
    /// <summary>Records one run as the current caller.</summary>
    /// <param name="service">The service that ran.</param>
    /// <param name="run">The run: the grant it is fenced by and its slot.</param>
    /// <param name="outcome">Success, or the error it failed with.</param>
    /// <param name="job">The job of a manual run, which keys the record; null for a scheduled run.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The ledger sequence of the record, or why it was not written.</returns>
    public ValueTask<Result<long>> Record(ServiceName service, ScheduledRun run, Result<Unit> outcome, JobId? job, CancellationToken cancellationToken);
}
