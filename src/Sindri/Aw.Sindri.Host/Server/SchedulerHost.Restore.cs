using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Server;

internal sealed partial class SchedulerHost
{
    /// <summary>The outcome of <paramref name="name"/>'s latest 'service.ran' in the ledger, or null when it has none or the ledger can't be read (then the run is recorded).</summary>
    private async Task<ServiceOutcome?> Restore(IServiceProvider services, ServiceName name, CancellationToken ct)
    {
        var reader = services.GetService<ILedgerReader>();
        var events = services.GetService<EventFactory>();
        if (reader is null || events is null)
        {
            return null;
        }

        ServiceOutcome? latest = null;
        try
        {
            for (var after = 0L; ;)
            {
                var page = await reader.ReadAfter(after, 500, ct).ConfigureAwait(false);
                if (page.IsError || page.Value.Count == 0)
                {
                    return page.IsError ? null : latest;
                }

                foreach (var e in page.Value.Where(e => string.Equals(e.Envelope.Type, EventTypes.ServiceRan, StringComparison.Ordinal)))
                {
                    var ran = events.Payload<ServiceRan>(e);
                    if (ran.IsSuccess && ran.Value.Service == name)
                    {
                        latest = ran.Value.Outcome;
                    }
                }

                after = page.Value[^1].Sequence;
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: an unreadable ledger must not stop the schedule; the run is recorded instead
        {
            LogRestoreFailed(log, name.Value, e.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}': its last recorded run couldn't be read back ({Detail}); recording this run")]
    private static partial void LogRestoreFailed(ILogger logger, string service, string detail);
}
