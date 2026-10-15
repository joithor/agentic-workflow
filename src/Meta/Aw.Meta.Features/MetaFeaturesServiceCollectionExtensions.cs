using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Features.Health;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Features.InboxDefer;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Features.LogAppend;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.Owner;
using Aw.Meta.Features.OwnerAnswer;
using Aw.Meta.Features.OwnerAsk;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Features.Team;
using Aw.Meta.Features.Watch;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Features;

/// <summary>Registers Aw.Meta.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class MetaFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Meta use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaFeatures(this IServiceCollection services) => services
        .AddSetupSlices()
        .AddInboxSlices()
        .AddHookSlices()
        .AddOwnerSlices()
        .AddReportSlices()
        .AddGateSlices()
        .AddHostSlices();

    private static IServiceCollection AddSetupSlices(this IServiceCollection services) => services
        .AddScoped<SetRoleService>()
        .Plain<SetRoleService, SetRoleRequest, MetaRole>()
        .AddScoped<ShowRoleService>()
        .Plain<ShowRoleService, ShowRoleRequest, ShowRoleResponse>()
        .AddScoped<TeamInitService>()
        .AddScoped<IValidator<TeamInitRequest>, TeamInitValidator>()
        .Validated<TeamInitService, TeamInitRequest, TeamConfig>()
        .AddScoped<TeamShowService>()
        .Plain<TeamShowService, TeamShowRequest, TeamConfig>();

    private static IServiceCollection AddInboxSlices(this IServiceCollection services) => services
        .AddScoped<SyncInboxService>()
        .AddScoped<IValidator<SyncInboxRequest>, SyncInboxValidator>()
        .Validated<SyncInboxService, SyncInboxRequest, SyncInboxResponse>()
        .AddScoped<ListInboxService>()
        .Plain<ListInboxService, ListInboxRequest, ListInboxResponse>()
        .AddScoped<DismissInboxService>()
        .AddScoped<IValidator<DismissInboxRequest>, DismissInboxValidator>()
        .Validated<DismissInboxService, DismissInboxRequest, DismissInboxResponse>()
        .AddScoped<DeferInboxService>()
        .AddScoped<IValidator<DeferInboxRequest>, DeferInboxValidator>()
        .Validated<DeferInboxService, DeferInboxRequest, DeferInboxResponse>();

    private static IServiceCollection AddHookSlices(this IServiceCollection services) => services
        .AddScoped<StopCheckService>()
        .AddScoped<IValidator<StopCheckRequest>, StopCheckValidator>()
        .Validated<StopCheckService, StopCheckRequest, StopCheckResponse>()
        .AddScoped<OutboxRecordService>()
        .AddScoped<IValidator<OutboxRecordRequest>, OutboxRecordValidator>()
        .Validated<OutboxRecordService, OutboxRecordRequest, OutboxRecordResponse>()
        .AddScoped<RefreshLanesService>()
        .Plain<RefreshLanesService, RefreshLanesRequest, LanesResponse>();

    private static IServiceCollection AddOwnerSlices(this IServiceCollection services) => services
        .AddScoped<ListOwnerService>()
        .Plain<ListOwnerService, ListOwnerRequest, ListOwnerResponse>()
        .AddScoped<AnswerOwnerService>()
        .AddScoped<IValidator<AnswerOwnerRequest>, AnswerOwnerValidator>()
        .Validated<AnswerOwnerService, AnswerOwnerRequest, AnswerOwnerResponse>()
        .AddScoped<AskOwnerService>()
        .AddScoped<IValidator<AskOwnerRequest>, AskOwnerValidator>()
        .Validated<AskOwnerService, AskOwnerRequest, AskOwnerResponse>();

    private static IServiceCollection AddReportSlices(this IServiceCollection services) => services
        .AddScoped<AppendLogService>()
        .AddScoped<IValidator<AppendLogRequest>, AppendLogValidator>()
        .Validated<AppendLogService, AppendLogRequest, AppendLogResponse>()
        .AddScoped<StatusReader>()
        .AddScoped<StatusService>()
        .Plain<StatusService, StatusRequest, StatusResponse>();

    private static IServiceCollection AddGateSlices(this IServiceCollection services) => services
        .AddScoped<GateRunService>()
        .AddScoped<IValidator<GateRunRequest>, GateRunValidator>()
        .Validated<GateRunService, GateRunRequest, GateRunResponse>()
        .AddScoped<GateRerunService>()
        .AddScoped<IValidator<GateRerunRequest>, GateRerunValidator>()
        .Validated<GateRerunService, GateRerunRequest, GateRerunResponse>()
        .AddScoped<CheckMergeService>()
        .AddScoped<IValidator<CheckMergeRequest>, CheckMergeValidator>()
        .Validated<CheckMergeService, CheckMergeRequest, CheckMergeResponse>()
        .AddScoped<MergeRunService>()
        .AddScoped<IValidator<MergeRunRequest>, MergeRunValidator>()
        .Validated<MergeRunService, MergeRunRequest, MergeRunResponse>();

    private static IServiceCollection AddHostSlices(this IServiceCollection services) => services
        .AddScoped<HealthService>()
        .Plain<HealthService, HealthRequest, HealthResponse>()
        .AddScoped<WatchService>()
        .AddScoped<IValidator<WatchRequest>, WatchValidator>()
        .Validated<WatchService, WatchRequest, WatchResponse>();

    // Exposes an already registered service as its IService.
    private static IServiceCollection Plain<TService, TRequest, TResponse>(this IServiceCollection services)
        where TService : class, IService<TRequest, TResponse>
        where TRequest : notnull =>
        services.AddScoped<IService<TRequest, TResponse>>(static sp => sp.GetRequiredService<TService>());

    // Exposes an already registered service, with its registered validator in front, as its IService.
    private static IServiceCollection Validated<TService, TRequest, TResponse>(this IServiceCollection services)
        where TService : class, IService<TRequest, TResponse>
        where TRequest : notnull =>
        services.AddScoped<IService<TRequest, TResponse>>(static sp =>
            new ValidatingService<TRequest, TResponse>(sp.GetRequiredService<TService>(), sp.GetRequiredService<IValidator<TRequest>>()));
}
