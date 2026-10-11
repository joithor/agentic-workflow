using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure;

/// <summary>Registers Aw.Sindri.Infrastructure's adapters and settings. A second technology for a port gets its own AddSindri&lt;Tech&gt;Infrastructure method.</summary>
public static class SindriInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Sindri adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
