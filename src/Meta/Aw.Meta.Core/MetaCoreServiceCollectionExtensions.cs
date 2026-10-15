using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Core;

/// <summary>Registers Aw.Meta.Core's types.</summary>
public static class MetaCoreServiceCollectionExtensions
{
    /// <summary>Adds the Meta domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaCore(this IServiceCollection services) => services;
}
