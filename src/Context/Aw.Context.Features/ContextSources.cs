using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Features.SyncSources;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The source verbs of the facade: status, the docs refresh and the sync (kept apart so the engine stays under the constructor limit).</summary>
internal sealed class ContextSources(
    IService<SourcesStatusRequest, SourcesView> status,
    IService<RefreshDocsRequest, RefreshReport> refresh,
    IService<SyncSourcesRequest, SyncReport> sync)
{
    public ValueTask<Result<SourcesView>> Status(ContextScope scope, CancellationToken cancellationToken) =>
        status.Handle(new SourcesStatusRequest(scope), cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        refresh.Handle(new RefreshDocsRequest(scope), cancellationToken);

    public async ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken)
    {
        if (source is not null && !SourceId.TryFrom(source, out _))
        {
            return ContextErrors.SyncSourceUnknown.With($"'{source}' isn't a source id (fix: sindri context sources status)");
        }

        var rows = new List<SourceSync>();
        if (source is null || string.Equals(source, RepoDocs.Id.Value, StringComparison.Ordinal))
        {
            var refreshed = await refresh.Handle(new RefreshDocsRequest(scope), cancellationToken).ConfigureAwait(false);
            if (refreshed.IsError)
            {
                return refreshed.Error;
            }

            rows.Add(DocsRow(refreshed.Value));
        }

        if (source is null || !string.Equals(source, RepoDocs.Id.Value, StringComparison.Ordinal))
        {
            var synced = await sync.Handle(new SyncSourcesRequest(scope, source is null ? null : SourceId.From(source)), cancellationToken).ConfigureAwait(false);
            if (synced.IsError)
            {
                return synced.Error;
            }

            rows.AddRange(synced.Value.Sources);
        }

        return new SyncReport(rows);
    }

    private static SourceSync DocsRow(RefreshReport report) =>
        new(report.Source, report.Repos.Sum(r => r.Added + r.Kept), report.Repos.Sum(r => r.Added), report.Repos.Sum(r => r.Skipped), 0, 0, 0, 0, 0, 0, 0,
            report.Repos.Select(r => r.Code).FirstOrDefault(c => c is not null));
}
