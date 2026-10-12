using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.ShowMapStatus;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The map part of the facade: each method dispatches to its slice (spec §4).</summary>
internal sealed class ContextMap(
    IService<PreviewMapRequest, MapFilePreview> preview,
    IService<ImportMapRequest, MapImported> import,
    IService<ExportMapRequest, MapExported> export,
    IService<ShowMapStatusRequest, MapStatusView> status,
    IService<CheckMapRequest, MapHealth> checkMap,
    IService<CheckRetentionRequest, MapHealth> checkRetention,
    IService<PurgeMapRequest, PurgeReport> purge) : IContextMap
{
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, CancellationToken cancellationToken) =>
        preview.Handle(new PreviewMapRequest(path), cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, CancellationToken cancellationToken) =>
        import.Handle(new ImportMapRequest(path, confirmedHash), cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        export.Handle(new ExportMapRequest(path, scope), cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(ContextScope scope, CancellationToken cancellationToken) =>
        status.Handle(new ShowMapStatusRequest(scope), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(CancellationToken cancellationToken) =>
        checkMap.Handle(new CheckMapRequest(), cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        checkRetention.Handle(new CheckRetentionRequest(), cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) =>
        purge.Handle(new PurgeMapRequest(), cancellationToken);
}
