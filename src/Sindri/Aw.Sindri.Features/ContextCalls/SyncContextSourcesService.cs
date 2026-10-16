using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a sync job (context spec §8.8; API P6): syncs the intent sources through the engine under the starting caller, which is the same work as
/// the 'context-sync' service, and records 'context.sources.synced' per source (slot: when the job began). A source that is down is a row with its
/// code, as in the service; a sync of a source that is running already (the scheduled service's) is SND-CONTEXT-056 and fails the job.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="jobs">The running job: its progress takes a line.</param>
/// <param name="clock">Dates the slot.</param>
internal sealed class SyncContextSourcesService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IJobContextAccessor jobs, TimeProvider clock)
    : IService<SyncContextSourcesRequest, SyncContextSourcesResult>
{
    public async ValueTask<Result<SyncContextSourcesResult>> Handle(SyncContextSourcesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var slot = clock.GetUtcNow();
        jobs.Current?.Progress.Report(request.Source is null ? "syncing every source" : $"syncing {request.Source}");
        var report = await engine.Sync(request.Source, scope.Value, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return report.Error;
        }

        foreach (var row in report.Value.Sources)
        {
            var recorded = await Record(row, slot, grant, cancellationToken).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }
        }

        return new SyncContextSourcesResult([.. report.Value.Sources.Select(static r => new SyncedSource(r.Source.Value, Links(r)))]);
    }

    private static ContextLinksSynced Links(SourceSync row) =>
        new(row.Records, row.Fetched, row.Unchanged, row.Linked, row.Unlinked, row.Orphaned, row.Decisions, row.Queued, row.Refused, row.Tokens, row.Code);

    private async ValueTask<Result<Unit>> Record(SourceSync row, DateTimeOffset slot, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var payload = new ContextSourcesSynced(row.Source.Value, slot, [], Links(row));
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextSourcesSynced}:{grant.Epoch}:{row.Source.Value}:{slot:O}");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextSourcesSynced, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
