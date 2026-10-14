using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RecordServiceRun;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Aw.Sindri.Features's slices: the command layer every client (CLI, API, UI) calls (factory addendum §17.27.1).</summary>
public static class SindriFeaturesServiceCollectionExtensions
{
    /// <summary>
    /// Adds every Sindri use case as an IService: commands and queries behind their chains (API spec §2.1), the host's
    /// own services and the CLI-local verbs as plain services, the background services as IScheduledService, and the
    /// workflow blocks this build ships.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services) =>
        services.AddServices().AddCommands().AddQueries();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IService<StartProcessRequest, StartProcessResponse>, StartProcessService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IService<RecordServiceRunRequest, RecordServiceRunResponse>, RecordServiceRunService>()
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddScoped<ProfileFacts>()
            .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>, RunDoctorService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddScoped<ContextScopes>()
            .AddScoped<IScheduledService, ContextDocsRefreshService>()
            .AddScoped<IScheduledService, ContextPurgeService>()
            .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
            .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
            .AddRecipeBlocks();
        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // ---- commands (one line per slice) ----
        _ = services.AddScoped<IValidator<CloseItemRequest>, CloseItemValidator>().AddCommand<CloseItemRequest, CommandAccepted, CloseItemService>();
        _ = services.AddScoped<IValidator<CreateItemRequest>, CreateItemValidator>().AddCommand<CreateItemRequest, ItemCreatedResponse, CreateItemService>();
        _ = services.AddScoped<IValidator<RebuildViewsRequest>, AcceptAllValidator<RebuildViewsRequest>>().AddCommand<RebuildViewsRequest, CommandAccepted, RebuildViewsService>();
        _ = services.AddScoped<IValidator<ImportContextMapRequest>, AcceptAllValidator<ImportContextMapRequest>>().AddCommand<ImportContextMapRequest, ImportContextMapResponse, ImportContextMapService>();
        _ = services.AddScoped<IValidator<ApproveProfileRequest>, AcceptAllValidator<ApproveProfileRequest>>().AddCommand<ApproveProfileRequest, ApproveProfileResponse, ApproveProfileService>();
        _ = services.AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddCommand<StopProcessRequest, StopProcessResponse, StopProcessService>();
        return services;
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        // ---- queries (one line per slice) ----
        _ = services.AddScoped<IValidator<GetEventCatalogRequest>, AcceptAllValidator<GetEventCatalogRequest>>().AddQuery<GetEventCatalogRequest, EventCatalogView, GetEventCatalogService>();
        _ = services.AddScoped<IValidator<GetItemRequest>, AcceptAllValidator<GetItemRequest>>().AddQuery<GetItemRequest, ItemView, GetItemService>();
        _ = services.AddScoped<IValidator<GetItemTimelineRequest>, AcceptAllValidator<GetItemTimelineRequest>>().AddQuery<GetItemTimelineRequest, TimelineResponse, GetItemTimelineService>();
        _ = services.AddScoped<IValidator<GetProfileRequest>, AcceptAllValidator<GetProfileRequest>>().AddQuery<GetProfileRequest, GetProfileResponse, GetProfileService>();
        _ = services.AddScoped<IValidator<GetQueueRequest>, AcceptAllValidator<GetQueueRequest>>().AddQuery<GetQueueRequest, QueueResponse, GetQueueService>();
        _ = services.AddScoped<IValidator<ExportContextMapRequest>, AcceptAllValidator<ExportContextMapRequest>>().AddQuery<ExportContextMapRequest, MapExported, ExportContextMapService>();
        _ = services.AddScoped<IValidator<GetContextMapRequest>, AcceptAllValidator<GetContextMapRequest>>().AddQuery<GetContextMapRequest, MapStatusView, GetContextMapService>();
        _ = services.AddScoped<IValidator<GetContextSourcesRequest>, AcceptAllValidator<GetContextSourcesRequest>>().AddQuery<GetContextSourcesRequest, SourcesView, GetContextSourcesService>();
        _ = services.AddScoped<IValidator<GetStatusRequest>, AcceptAllValidator<GetStatusRequest>>().AddQuery<GetStatusRequest, GetStatusResponse, GetStatusService>();
        _ = services.AddScoped<IValidator<ListCommandsRequest>, AcceptAllValidator<ListCommandsRequest>>().AddQuery<ListCommandsRequest, ListCommandsResponse, ListCommandsService>();
        _ = services.AddScoped<IValidator<ListItemsRequest>, AcceptAllValidator<ListItemsRequest>>().AddQuery<ListItemsRequest, ListItemsResponse, ListItemsService>();
        _ = services.AddScoped<IValidator<ListServicesRequest>, AcceptAllValidator<ListServicesRequest>>().AddQuery<ListServicesRequest, ListServicesResponse, ListServicesService>();
        _ = services.AddScoped<IValidator<PingProcessRequest>, AcceptAllValidator<PingProcessRequest>>().AddQuery<PingProcessRequest, Ping, PingProcessService>();
        _ = services.AddScoped<IValidator<QueryContextRequest>, AcceptAllValidator<QueryContextRequest>>().AddQuery<QueryContextRequest, ContextResult, QueryContextService>();
        _ = services.AddScoped<IValidator<ReadLedgerRequest>, AcceptAllValidator<ReadLedgerRequest>>().AddQuery<ReadLedgerRequest, LedgerPage, ReadLedgerService>();
        _ = services.AddScoped<IValidator<StreamEventsRequest>, AcceptAllValidator<StreamEventsRequest>>().AddQuery<StreamEventsRequest, StreamEventsResponse, StreamEventsService>();
        return services;
    }
}
