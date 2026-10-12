using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features;

/// <summary>Registers Aw.Context.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class ContextFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Context use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextFeatures(this IServiceCollection services) => services;
}
