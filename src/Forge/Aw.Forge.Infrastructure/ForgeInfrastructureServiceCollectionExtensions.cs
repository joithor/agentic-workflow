using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Infrastructure;

/// <summary>Registers Aw.Forge.Infrastructure's adapters and settings. A second technology for a port gets its own AddForge&lt;Tech&gt;Infrastructure method.</summary>
public static class ForgeInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Forge adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddForgeInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
