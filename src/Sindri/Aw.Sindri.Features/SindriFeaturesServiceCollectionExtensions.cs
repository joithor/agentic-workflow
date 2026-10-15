using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RecordServiceRun;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowRecipe;
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
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IService<StartProcessRequest, StartProcessResponse>, StartProcessService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IService<RecordServiceRunRequest, RecordServiceRunResponse>, RecordServiceRunService>()
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddScoped<ProfileFacts>()
            .AddScoped<LedgerFacts>()
            .AddScoped<RunDoctorService>()
            .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>>(static sp =>
                new WithMoreChecks(sp.GetRequiredService<RunDoctorService>(), sp.GetServices<IDoctorChecks>()))
            .AddScoped<IDoctorChecks, ContextDoctorChecks>()
            .AddScoped<IService<RegisterMcpRequest, RegisterMcpResponse>, RegisterMcpService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddScoped<ContextScopes>()
            .AddScoped<IScheduledService, ContextDocsRefreshService>()
            .AddScoped<IScheduledService, ContextPurgeService>()
            .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
            .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
            .AddRecipeBlocks()
            .AddRegistrySlices();

        // ---- commands (one line per slice) ----
        _ = services.AddScoped<IValidator<RebuildViewsRequest>, AcceptAllValidator<RebuildViewsRequest>>().AddCommand<RebuildViewsRequest, CommandAccepted, RebuildViewsService>();
        _ = services.AddScoped<IValidator<ImportContextMapRequest>, AcceptAllValidator<ImportContextMapRequest>>().AddCommand<ImportContextMapRequest, ImportContextMapResponse, ImportContextMapService>();
        _ = services.AddScoped<IValidator<ApproveProfileRequest>, AcceptAllValidator<ApproveProfileRequest>>().AddCommand<ApproveProfileRequest, ApproveProfileResponse, ApproveProfileService>();
        _ = services.AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddCommand<StopProcessRequest, StopProcessResponse, StopProcessService>();

        // ---- queries (one line per slice) ----
        _ = services.AddScoped<IValidator<GetProfileRequest>, AcceptAllValidator<GetProfileRequest>>().AddQuery<GetProfileRequest, GetProfileResponse, GetProfileService>();
        _ = services.AddScoped<IValidator<ExportContextMapRequest>, AcceptAllValidator<ExportContextMapRequest>>().AddQuery<ExportContextMapRequest, MapExported, ExportContextMapService>();
        _ = services.AddScoped<IValidator<GetContextMapRequest>, AcceptAllValidator<GetContextMapRequest>>().AddQuery<GetContextMapRequest, MapStatusView, GetContextMapService>();
        _ = services.AddScoped<IValidator<GetContextSourcesRequest>, AcceptAllValidator<GetContextSourcesRequest>>().AddQuery<GetContextSourcesRequest, SourcesView, GetContextSourcesService>();
        _ = services.AddScoped<IValidator<GetStatusRequest>, AcceptAllValidator<GetStatusRequest>>().AddQuery<GetStatusRequest, GetStatusResponse, GetStatusService>();
        _ = services.AddScoped<IValidator<ListCommandsRequest>, AcceptAllValidator<ListCommandsRequest>>().AddQuery<ListCommandsRequest, ListCommandsResponse, ListCommandsService>();
        _ = services.AddScoped<IValidator<ListServicesRequest>, AcceptAllValidator<ListServicesRequest>>().AddQuery<ListServicesRequest, ListServicesResponse, ListServicesService>();
        _ = services.AddScoped<IValidator<PingProcessRequest>, AcceptAllValidator<PingProcessRequest>>().AddQuery<PingProcessRequest, Ping, PingProcessService>();
        _ = services.AddScoped<IValidator<QueryContextRequest>, AcceptAllValidator<QueryContextRequest>>().AddQuery<QueryContextRequest, ContextResult, QueryContextService>();
        _ = services.AddScoped<IValidator<StreamEventsRequest>, AcceptAllValidator<StreamEventsRequest>>().AddQuery<StreamEventsRequest, StreamEventsResponse, StreamEventsService>();
        return services;
    }

    /// <summary>The registry's slices (engine P2): register, approve and revoke (commands), the challenge (an issue), list and show (queries).</summary>
    private static IServiceCollection AddRegistrySlices(this IServiceCollection services)
    {
        _ = services.AddScoped<IValidator<RegisterRecipeRequest>, AcceptAllValidator<RegisterRecipeRequest>>().AddCommand<RegisterRecipeRequest, RegisterRecipeResponse, RegisterRecipeService>();
        _ = services.AddScoped<IValidator<ApproveRecipeRequest>, AcceptAllValidator<ApproveRecipeRequest>>().AddCommand<ApproveRecipeRequest, ApproveRecipeResponse, ApproveRecipeService>();
        _ = services.AddScoped<IValidator<RevokeRecipeRequest>, AcceptAllValidator<RevokeRecipeRequest>>().AddCommand<RevokeRecipeRequest, RevokeRecipeResponse, RevokeRecipeService>();
        _ = services.AddScoped<IValidator<IssueChallengeRequest>, AcceptAllValidator<IssueChallengeRequest>>().AddIssue<IssueChallengeRequest, IssueChallengeResponse, IssueChallengeService>();
        _ = services.AddScoped<IValidator<ListRecipesRequest>, AcceptAllValidator<ListRecipesRequest>>().AddQuery<ListRecipesRequest, ListRecipesResponse, ListRecipesService>();
        _ = services.AddScoped<IValidator<ShowRecipeRequest>, AcceptAllValidator<ShowRecipeRequest>>().AddQuery<ShowRecipeRequest, ShowRecipeResponse, ShowRecipeService>();
        return services;
    }
}
