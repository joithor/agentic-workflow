using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.Core;

/// <summary>Registers Aw.Readiness.Core's types.</summary>
public static class ReadinessCoreServiceCollectionExtensions
{
    /// <summary>Adds the Readiness domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddReadinessCore(this IServiceCollection services) => services;
}
