using Aw.Meta.Core.Coordination;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Core;

/// <summary>Registers Aw.Meta.Core's types.</summary>
public static class MetaCoreServiceCollectionExtensions
{
    /// <summary>Adds the Meta domain services that the slices share: the inbox sync, the lane refresher, the obligation reader and the meta-session check.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaCore(this IServiceCollection services) => services
        .AddScoped<InboxSync>()
        .AddScoped<LaneRefresher>()
        .AddScoped<ObligationReader>()
        .AddScoped<MetaSession>();
}
