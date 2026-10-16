using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RecordServiceRun;

/// <summary>
/// Appends 'service.ran' for one scheduled run (or a manual one, RunService), ok or failed with its error code (API spec C8), under the run's caller
/// ('service:&lt;name&gt;'), keyed by service, lease epoch and slot so a replayed slot records once.
/// </summary>
internal sealed class RecordServiceRunService(ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers, IIdGenerator ids)
    : IService<RecordServiceRunRequest, RecordServiceRunResponse>, IServiceRunRecorder
{
    public async ValueTask<Result<RecordServiceRunResponse>> Handle(RecordServiceRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recorded = await Record(request.Service, request.Run, request.Outcome, request.Job, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new RecordServiceRunResponse(recorded.Value);
    }

    public async ValueTask<Result<long>> Record(ServiceName service, ScheduledRun run, Result<Unit> outcome, JobId? job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(outcome);
        var failed = outcome.IsError;
        var payload = new ServiceRan(callers.Current?.CommandId ?? ids.New<CommandId>(), service, run.Slot,
            failed ? ServiceOutcome.Failed : ServiceOutcome.Ok, failed ? outcome.Error.Code : null, job);
        var entry = events.Entry(EventTypes.ServiceRan, payload,
            job is { } manual
                ? $"{EventTypes.ServiceRan}:{service.Value}:manual:{manual.Value}"   // a manual run is keyed by its job, so a replayed job records once
                : string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ServiceRan}:{service.Value}:{run.Grant.Epoch}:{run.Slot:O}"));
        var recorded = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : recorded.Value.Sequence;
    }
}
