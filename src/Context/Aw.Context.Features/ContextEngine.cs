using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.SourcesStatus;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The facade (spec §4): each method dispatches to its slice; settings are read the same way by all of them.</summary>
internal sealed class ContextEngine(
    IService<QueryContextRequest, ContextResult> query,
    IService<SourcesStatusRequest, SourcesView> sources,
    IService<RefreshDocsRequest, RefreshReport> refresh) : IContextEngine
{
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        SettingsReader.Check(section, repos);

    public ValueTask<Result<ContextResult>> Query(ContextQuery query1, ContextScope scope, CancellationToken cancellationToken) =>
        query.Handle(new QueryContextRequest(query1, scope), cancellationToken);

    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        sources.Handle(new SourcesStatusRequest(scope), cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        refresh.Handle(new RefreshDocsRequest(scope), cancellationToken);
}
