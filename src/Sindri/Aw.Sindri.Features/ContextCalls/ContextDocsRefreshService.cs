using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-docs-refresh' background service (context spec §5.2, §8.8): runs only when the profile names it, first
/// at serve start, then on its cadence. It brings the repo-docs index up to date with each repo's HEAD and records
/// 'context.sources.synced' with the counts, keyed by the run's slot. Until stage S-5B adds the SyncSources job
/// (after API P6), it is the only way the index refreshes: that row is pending:S-5B.
/// </summary>
internal sealed class ContextDocsRefreshService(ContextScopes scopes, IContextEngine engine, ILedgerStore ledger, EventFactory events) : IScheduledService
{
    /// <summary>The service's name in the profile.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-docs-refresh");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var report = scope.IsError ? scope.Error : await engine.Refresh(scope.Value, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return report.Error;
        }

        var payload = new ContextSourcesSynced(report.Value.Source.Value, run.Slot,
            [.. report.Value.Repos.Select(r => new ContextRepoSynced(r.Repo.Value, r.Head?.Value, r.Added, r.Removed, r.Kept, r.Blocked, r.Code, r.Skipped, r.Recleaned))]);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextSourcesSynced}:{run.Grant.Epoch}:{run.Slot:O}");
        var recorded = await ledger.Append(events.Entry(EventTypes.ContextSourcesSynced, payload, key), run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : Unit.Value;
    }
}
