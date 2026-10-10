using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Infrastructure;

/// <summary>Registers Aw.BugFix.Infrastructure's adapters and settings. A second technology for a port gets its own AddBugFix&lt;Tech&gt;Infrastructure method.</summary>
public static class BugFixInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the BugFix adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
