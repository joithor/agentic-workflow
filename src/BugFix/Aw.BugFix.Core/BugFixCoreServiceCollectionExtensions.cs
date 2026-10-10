using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Core;

/// <summary>Registers Aw.BugFix.Core's types.</summary>
public static class BugFixCoreServiceCollectionExtensions
{
    /// <summary>Adds the BugFix domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixCore(this IServiceCollection services) => services;
}
