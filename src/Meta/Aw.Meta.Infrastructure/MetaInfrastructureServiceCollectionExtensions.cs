using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Infrastructure;

/// <summary>Registers Aw.Meta.Infrastructure's adapters and settings. A second technology for a port gets its own AddMeta&lt;Tech&gt;Infrastructure method.</summary>
public static class MetaInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Meta adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
