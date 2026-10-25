using Microsoft.Extensions.DependencyInjection;

namespace Aw.CodeIndex.Core;

/// <summary>Registers Aw.CodeIndex.Core's types.</summary>
public static class CodeIndexCoreServiceCollectionExtensions
{
    /// <summary>Adds the CodeIndex domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCodeIndexCore(this IServiceCollection services) => services;
}
