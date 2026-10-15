using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.RecordServiceRun;

/// <summary>
/// Appends 'service.ran' for one scheduled run, ok or failed with its error code (API spec C8), under the run's caller
/// ('service:&lt;name&gt;'), keyed by service, lease epoch and slot so a replayed slot records once.
/// </summary>
internal sealed class RecordServiceRunService(ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers, IIdGenerator ids)
    : IService<RecordServiceRunRequest, RecordServiceRunResponse>
{
    public async ValueTask<Result<RecordServiceRunResponse>> Handle(RecordServiceRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = request.Run;
        var failed = request.Outcome.IsError;
        var payload = new ServiceRan(callers.Current?.CommandId ?? ids.New<CommandId>(), request.Service, run.Slot,
            failed ? ServiceOutcome.Failed : ServiceOutcome.Ok, failed ? request.Outcome.Error.Code : null);
        var entry = events.Entry(EventTypes.ServiceRan, payload,
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ServiceRan}:{request.Service.Value}:{run.Grant.Epoch}:{run.Slot:O}"));
        var recorded = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new RecordServiceRunResponse(recorded.Value.Sequence);
    }
}
