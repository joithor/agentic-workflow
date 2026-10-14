using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.SourcesStatus;

/// <summary>
/// The sources' state: one repo-docs row per repo in scope, from its index (never refreshed reads as degraded, a
/// refresh that failed as unavailable), then every live source's probe. A session's view is gated and scoped like its
/// queries, but says whether sessions are enabled.
/// </summary>
internal sealed class SourcesStatusService(IDocIndex index, ISourceCatalog catalog, TimeProvider clock) : IService<SourcesStatusRequest, SourcesView>
{
    public async ValueTask<Result<SourcesView>> Handle(SourcesStatusRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var repos = settings.Bind(s => ScopeRules.Resolve(s, request.Scope.Repos, request.Scope.Caller));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var rows = new List<SourceStatusRow>();
        foreach (var repo in repos.Value)
        {
            var status = await index.Status(repo, cancellationToken).ConfigureAwait(false);
            if (status.IsError)
            {
                return status.Error;
            }

            rows.Add(Row(repo.Name, status.Value));
        }

        foreach (var source in catalog.For(settings.Value, repos.Value).Where(s => s.Describe().Freshness == SourceFreshness.Live))
        {
            var health = await source.Probe(cancellationToken).ConfigureAwait(false);
            var described = source.Describe();
            rows.Add(new SourceStatusRow(described.Id, described.Kind, Repo: null, health.State, health.Detail, health.Code, LastSync: null, Records: 0));
        }

        return new SourcesView(rows, settings.Value.SessionsEnabled, clock.GetUtcNow());
    }

    private static SourceStatusRow Row(ContextRepoName repo, DocIndexStatus status)
    {
        var head = status.Head is { } h ? h.Value[..Math.Min(7, h.Value.Length)] : "none";
        var (state, detail) = (status.LastSync, status.Code) switch
        {
            (null, null) => (SourceState.Degraded, "never refreshed (fix: name the context-docs-refresh service in the profile)"),
            (_, { } code) => (SourceState.Unavailable, $"the last refresh failed ({code})"),
            _ => (SourceState.Ok, string.Create(CultureInfo.InvariantCulture, $"{status.Documents} documents, {status.Chunks} chunks at {head}")),
        };
        return new SourceStatusRow(RepoDocs.Id, SourceKind.Doc, repo, state, detail, status.Code, status.LastSync, status.Chunks);
    }
}
