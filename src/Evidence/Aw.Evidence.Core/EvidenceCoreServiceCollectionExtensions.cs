using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Core;

/// <summary>Registers Aw.Evidence.Core's types.</summary>
public static class EvidenceCoreServiceCollectionExtensions
{
    /// <summary>Adds the Evidence domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceCore(this IServiceCollection services) => services;
}
