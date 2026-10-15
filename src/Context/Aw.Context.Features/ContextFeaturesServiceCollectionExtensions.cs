using Aw.Context.Core.Eval;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Features.ApplyReviewFile;
using Aw.Context.Features.BuildMap;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ExportReview;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.GetProposals;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PromoteMap;
using Aw.Context.Features.ProposeFromScope;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.ReviewMap;
using Aw.Context.Features.ReviewQueue;
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
        .AddScoped<IService<EvalProbesRequest, ProbeRun>, EvalProbesService>()
        .AddScoped<IService<BuildMapRequest, MapBuildReport>, BuildMapService>()
        .AddScoped<IService<ReviewQueueRequest, ReviewQueueView>, ReviewQueueService>()
        .AddScoped<IService<ReviewMapRequest, ReviewResult>, ReviewMapService>()
        .AddScoped<IService<ExportReviewRequest, ReviewExported>, ExportReviewService>()
        .AddScoped<IService<ApplyReviewFileRequest, ReviewResult>, ApplyReviewFileService>()
        .AddScoped<IService<PromoteMapRequest, MapPromoted>, PromoteMapService>()
        .AddScoped<IService<ProposeFromScopeRequest, ProposeResult>, ProposeFromScopeService>()
        .AddScoped<IService<GetProposalsRequest, ProposalsView>, GetProposalsService>()
        .AddScoped<IService<GetLensesRequest, LensesView>, GetLensesService>()
        .AddScoped<IService<GetFileMapRequest, FileMapView>, GetFileMapService>()
        .AddScoped<MapReader>()
        .AddScoped<MapAdmin>()
        .AddScoped<MapReview>()
        .AddScoped<MapProposals>()
        .AddScoped<IContextMap, ContextMap>()
        .AddScoped<ContextGraph>()
        .AddScoped<IContextGraph>(static sp => sp.GetRequiredService<ContextGraph>())
        .AddScoped<IContextLenses>(static sp => sp.GetRequiredService<ContextGraph>())
        .AddScoped<IContextEngine, ContextEngine>();
}
