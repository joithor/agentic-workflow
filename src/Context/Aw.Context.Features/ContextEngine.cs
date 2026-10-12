using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
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
    IService<RefreshDocsRequest, RefreshReport> refresh,
    IContextMap map) : IContextEngine
{
    public Result<ContextSettings> CheckSettings(JsonElement? section) => SettingsReader.Read(section);

    public ValueTask<Result<ContextResult>> Query(ContextQuery query1, ContextScope scope, CancellationToken cancellationToken) =>
        query.Handle(new QueryContextRequest(query1, scope), cancellationToken);

    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        sources.Handle(new SourcesStatusRequest(scope), cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        refresh.Handle(new RefreshDocsRequest(scope), cancellationToken);

    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, CancellationToken cancellationToken) => map.PreviewMap(path, cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, CancellationToken cancellationToken) =>
        map.ImportMap(path, confirmedHash, cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        map.ExportMap(path, scope, cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(ContextScope scope, CancellationToken cancellationToken) => map.MapStatus(scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(CancellationToken cancellationToken) => map.CheckMap(cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) => map.CheckRetention(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) => map.PurgeExpired(cancellationToken);
}
