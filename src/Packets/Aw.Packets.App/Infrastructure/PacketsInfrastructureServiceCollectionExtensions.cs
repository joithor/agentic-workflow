using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Registers Aw.Packets.App.Infrastructure's adapters and settings. A second technology for a port gets its own AddPackets&lt;Tech&gt;Infrastructure method.</summary>
internal static class PacketsInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Packets adapters and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPacketsInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services.AddSingleton(SettingsFromEnvironment.Read(environment));
}
