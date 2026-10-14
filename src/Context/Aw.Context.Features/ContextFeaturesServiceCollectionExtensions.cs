using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.Graph;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.ShowMapStatus;
using Aw.Context.Features.ShowNode;
using Aw.Context.Features.SourcesStatus;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features;

/// <summary>Registers Aw.Context.Features: the IContextEngine facade and the slices behind it.</summary>
public static class ContextFeaturesServiceCollectionExtensions
{
    /// <summary>Adds the facade (IContextEngine, and IContextMap behind it) and every Context use case as an IService. The ports come from Aw.Context.Infrastructure.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextFeatures(this IServiceCollection services) => services
        .AddScoped<IService<QueryContextRequest, ContextResult>, QueryContextService>()
        .AddScoped<IService<SourcesStatusRequest, SourcesView>, SourcesStatusService>()
        .AddScoped<IService<RefreshDocsRequest, RefreshReport>, RefreshDocsService>()
        .AddScoped<IService<PreviewMapRequest, MapFilePreview>, PreviewMapService>()
        .AddScoped<IService<ImportMapRequest, MapImported>, ImportMapService>()
        .AddScoped<IService<ExportMapRequest, MapExported>, ExportMapService>()
        .AddScoped<IService<ShowMapStatusRequest, MapStatusView>, ShowMapStatusService>()
        .AddScoped<IService<CheckMapRequest, MapHealth>, CheckMapService>()
        .AddScoped<IService<CheckRetentionRequest, MapHealth>, CheckRetentionService>()
        .AddScoped<IService<PurgeMapRequest, PurgeReport>, PurgeMapService>()
        .AddScoped<IService<ShowNodeRequest, NodeView>, ShowNodeService>()
        .AddScoped<IService<ListNeighborsRequest, NeighborsView>, ListNeighborsService>()
        .AddScoped<IService<FindPathRequest, PathView>, FindPathService>()
        .AddScoped<GraphReader>()
        .AddScoped<IContextMap, ContextMap>()
        .AddScoped<IContextGraph, ContextGraph>()
        .AddScoped<IContextEngine, ContextEngine>();
}
