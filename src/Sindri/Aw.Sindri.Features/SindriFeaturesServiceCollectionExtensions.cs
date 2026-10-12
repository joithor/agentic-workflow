using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.PingProcess;
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
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services)
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
            .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
            .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
            .AddScoped<IService<CompleteStepRequest, CompleteStepResponse>, CompleteStepService>()
            .AddRecipeBlocks();

        // ---- commands (one line per slice) ----
        _ = services.AddScoped<IValidator<RebuildViewsRequest>, AcceptAllValidator<RebuildViewsRequest>>().AddCommand<RebuildViewsRequest, CommandAccepted, RebuildViewsService>();
        _ = services.AddScoped<IValidator<ApproveProfileRequest>, AcceptAllValidator<ApproveProfileRequest>>().AddCommand<ApproveProfileRequest, ApproveProfileResponse, ApproveProfileService>();
        _ = services.AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddCommand<StopProcessRequest, StopProcessResponse, StopProcessService>();

        // ---- queries (one line per slice) ----
        _ = services.AddScoped<IValidator<GetProfileRequest>, AcceptAllValidator<GetProfileRequest>>().AddQuery<GetProfileRequest, GetProfileResponse, GetProfileService>();
        _ = services.AddScoped<IValidator<GetStatusRequest>, AcceptAllValidator<GetStatusRequest>>().AddQuery<GetStatusRequest, GetStatusResponse, GetStatusService>();
        _ = services.AddScoped<IValidator<ListCommandsRequest>, AcceptAllValidator<ListCommandsRequest>>().AddQuery<ListCommandsRequest, ListCommandsResponse, ListCommandsService>();
        _ = services.AddScoped<IValidator<ListServicesRequest>, AcceptAllValidator<ListServicesRequest>>().AddQuery<ListServicesRequest, ListServicesResponse, ListServicesService>();
        _ = services.AddScoped<IValidator<PingProcessRequest>, AcceptAllValidator<PingProcessRequest>>().AddQuery<PingProcessRequest, Ping, PingProcessService>();
        _ = services.AddScoped<IValidator<StreamEventsRequest>, AcceptAllValidator<StreamEventsRequest>>().AddQuery<StreamEventsRequest, StreamEventsResponse, StreamEventsService>();
        _ = services.AddScoped<IValidator<GetInstanceRequest>, AcceptAllValidator<GetInstanceRequest>>().AddQuery<GetInstanceRequest, GetInstanceResponse, GetInstanceService>();
        _ = services.AddScoped<IValidator<ListInstancesRequest>, AcceptAllValidator<ListInstancesRequest>>().AddQuery<ListInstancesRequest, ListInstancesResponse, ListInstancesService>();
        _ = services.AddScoped<IValidator<GetInstanceEventsRequest>, AcceptAllValidator<GetInstanceEventsRequest>>().AddQuery<GetInstanceEventsRequest, GetInstanceEventsResponse, GetInstanceEventsService>();
        return services;
    }
}
