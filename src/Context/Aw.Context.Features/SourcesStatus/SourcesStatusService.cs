using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SourcesStatus;

/// <summary>
/// The sources' state: one repo-docs row per repo in scope, from its index (never refreshed reads as degraded, a
/// refresh that failed as unavailable, documents still stored under other scrub rules as degraded until the next
/// refresh), then every live source's probe. A session's view is gated and scoped like its queries, but says whether
/// sessions are enabled.
/// </summary>
internal sealed class SourcesStatusService(IDocIndex index, ISourceCatalog catalog, IScrubberFactory scrubberFactory, IIntentStore intents, TimeProvider clock)
    : IService<SourcesStatusRequest, SourcesView>
{
    public async ValueTask<Result<SourcesView>> Handle(SourcesStatusRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var rows = new List<SourceStatusRow>();
        foreach (var repo in repos.Value)
        {
            var status = await index.Status(repo, scrubbers.Value.Ruleset, cancellationToken).ConfigureAwait(false);
            if (status.IsError)
            {
                return status.Error;
            }

            rows.Add(Row(repo.Name, status.Value));
        }

        var live = await LiveRows(catalog.For(settings.Value, repos.Value, scrubbers.Value)
            .Where(s => s.Describe().Freshness == SourceFreshness.Live && ScopeRules.MayRead(request.Scope.Caller, s.Describe())), cancellationToken).ConfigureAwait(false);
        if (live.IsError)
        {
            return live.Error;
        }

        rows.AddRange(live.Value);
        return new SourcesView(rows, settings.Value.SessionsEnabled, clock.GetUtcNow());
    }

    private async ValueTask<Result<IReadOnlyList<SourceStatusRow>>> LiveRows(IEnumerable<IContextSource> sources, CancellationToken cancellationToken)
    {
        var rows = new List<SourceStatusRow>();
        foreach (var source in sources)
        {
            var health = await source.Probe(cancellationToken).ConfigureAwait(false);
            var described = source.Describe();
            var row = new SourceStatusRow(described.Id, described.Kind, Repo: null, health.State, health.Detail, health.Code, LastSync: null, Records: 0);
            if (described.Kind == SourceKind.Intent)
            {
                var state = await intents.State(described.Id, cancellationToken).ConfigureAwait(false);
                if (state.IsError)
                {
                    return state.Error;
                }

                row = row with { Sync = SyncStatus(state.Value) };
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Unavailable after a failed run; labeling being off (042) only queues the extraction, so the links synced and it stays ok. While records
    /// are queued the code stays 042, run after run (a run that fetches nothing records no failure of its own).
    /// </summary>
    private static SourceSyncStatus SyncStatus(IntentSourceState state)
    {
        var failed = state.LastError is { } code && code != ContextErrors.LabelingUnavailable.Code;
        var health = failed ? SourceState.Unavailable : state.LastSync is null ? SourceState.Degraded : SourceState.Ok;
        return new SourceSyncStatus(state.LastSync, health, state.Records, state.Links, state.Decisions, state.Queued, state.LastError ?? (state.Queued > 0 ? ContextErrors.LabelingUnavailable.Code : null));
    }

    private static SourceStatusRow Row(ContextRepoName repo, DocIndexStatus status)
    {
        var head = status.Head is { } h ? h.Value[..Math.Min(7, h.Value.Length)] : "none";
        var (state, detail) = (status.LastSync, status.Code) switch
        {
            (null, null) => (SourceState.Degraded, "never refreshed (fix: name the context-docs-refresh service in the profile)"),
            (_, { } code) => (SourceState.Unavailable, $"the last refresh failed ({code})"),
            _ when status.Outdated > 0 => (SourceState.Degraded, string.Create(CultureInfo.InvariantCulture,
                $"{status.Outdated} of {status.Documents} documents wait for the next refresh: the scrub rules changed, and searches skip them until they are cleaned again")),
            _ => (SourceState.Ok, string.Create(CultureInfo.InvariantCulture, $"{status.Documents} documents, {status.Chunks} chunks at {head}")),
        };
        return new SourceStatusRow(RepoDocs.Id, SourceKind.Doc, repo, state, detail, status.Code, status.LastSync, status.Chunks);
    }
}
