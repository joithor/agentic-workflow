using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core;

/// <summary>Registers Aw.Sindri.Core's types.</summary>
public static class SindriCoreServiceCollectionExtensions
{
    /// <summary>Adds the Sindri domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriCore(this IServiceCollection services) => services;
}
