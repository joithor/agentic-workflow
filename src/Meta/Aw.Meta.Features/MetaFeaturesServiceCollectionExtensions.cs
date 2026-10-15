using Aw.Kernel.Services;
using Aw.Meta.Features.SyncInbox;
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
            .AddScoped<IService<SyncInboxRequest, SyncInboxResponse>>(static sp =>
                new ValidatingService<SyncInboxRequest, SyncInboxResponse>(
                    sp.GetRequiredService<SyncInboxService>(),
                    sp.GetRequiredService<IValidator<SyncInboxRequest>>()));
        return services;
    }
}
