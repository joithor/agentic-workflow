using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Features.Team;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Features;

/// <summary>Registers Aw.Meta.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class MetaFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Meta use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<SyncInboxService>()
            .AddScoped<IValidator<SyncInboxRequest>, SyncInboxValidator>()
            .Validated<SyncInboxService, SyncInboxRequest, SyncInboxResponse>()
            .AddScoped<SetRoleService>()
            .Plain<SetRoleService, SetRoleRequest, MetaRole>()
            .AddScoped<ShowRoleService>()
            .Plain<ShowRoleService, ShowRoleRequest, ShowRoleResponse>()
            .AddScoped<TeamInitService>()
            .AddScoped<IValidator<TeamInitRequest>, TeamInitValidator>()
            .Validated<TeamInitService, TeamInitRequest, TeamConfig>()
            .AddScoped<TeamShowService>()
            .Plain<TeamShowService, TeamShowRequest, TeamConfig>();
        return services;
    }

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
