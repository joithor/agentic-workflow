using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Core;

/// <summary>Registers Aw.Context.Core's types.</summary>
public static class ContextCoreServiceCollectionExtensions
{
    /// <summary>Adds the Context domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextCore(this IServiceCollection services) => services;
}
