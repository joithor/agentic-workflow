using Microsoft.Extensions.DependencyInjection;

namespace Aw.CodeIndex.Infrastructure;

/// <summary>Registers Aw.CodeIndex.Infrastructure's adapters and settings. A second technology for a port gets its own AddCodeIndex&lt;Tech&gt;Infrastructure method.</summary>
public static class CodeIndexInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the CodeIndex adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCodeIndexInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
