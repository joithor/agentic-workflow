using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Infrastructure;

/// <summary>Registers Aw.Evidence.Infrastructure's adapters and settings. A second technology for a port gets its own AddEvidence&lt;Tech&gt;Infrastructure method.</summary>
public static class EvidenceInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Evidence adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
