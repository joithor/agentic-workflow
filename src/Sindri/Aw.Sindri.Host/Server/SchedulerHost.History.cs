using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Host.Server;

internal sealed partial class SchedulerHost
{
    /// <summary>
    /// The slot of each service's latest 'service.ran', for a schedule being (re)built (a restart or a re-approval anchors
    /// the series on it instead of running every service at once). A ledger that can't be read, or a build without one,
    /// gives none: every service then starts now, as before, and the schedule is never held up.
    /// </summary>
    private async Task<IReadOnlyDictionary<ServiceName, DateTimeOffset>> LastRuns(CancellationToken ct)
    {
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var history = await History(scope.ServiceProvider, ct).ConfigureAwait(false);
                if (history is null)
                {
                    return new Dictionary<ServiceName, DateTimeOffset>();
                }

                if (history.IsError)
                {
                    LogLastRunsFailed(log, $"{history.Error.Code} {history.Error.Message}");
                    return new Dictionary<ServiceName, DateTimeOffset>();
                }

                return history.Value.ToDictionary(r => r.Key, r => r.Value.Slot);
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: an unreadable ledger must not hold up the schedule
        {
            LogLastRunsFailed(log, e.Message);
            return new Dictionary<ServiceName, DateTimeOffset>();
        }
    }

    /// <summary>The outcome of <paramref name="name"/>'s latest 'service.ran' in the ledger, or null when it has none or the ledger can't be read (then the run is recorded).</summary>
    private async Task<ServiceOutcome?> Restore(IServiceProvider services, ServiceName name, CancellationToken ct)
    {
        try
        {
            var history = await History(services, ct).ConfigureAwait(false);
            return history is { IsSuccess: true } && history.Value.TryGetValue(name, out var last) ? last.Outcome : null;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: an unreadable ledger must not stop the schedule; the run is recorded instead
        {
            LogRestoreFailed(log, name.Value, e.Message);
            return null;
        }
    }

    /// <summary>The ledger's run history, or null when this host has no ledger reader.</summary>
    private static async Task<Result<IReadOnlyDictionary<ServiceName, LastRun>>?> History(IServiceProvider services, CancellationToken ct)
    {
        var reader = services.GetService<ILedgerReader>();
        var events = services.GetService<EventFactory>();
        return reader is null || events is null ? null : await ServiceRunHistory.Read(reader, events, ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}': its last recorded run couldn't be read back ({Detail}); recording this run")]
    private static partial void LogRestoreFailed(ILogger logger, string service, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the last runs of the services couldn't be read ({Detail}); every service starts now")]
    private static partial void LogLastRunsFailed(ILogger logger, string detail);
}
