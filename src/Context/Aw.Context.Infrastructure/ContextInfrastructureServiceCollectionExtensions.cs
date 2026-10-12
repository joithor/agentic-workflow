using Aw.Context.Infrastructure.Onyx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Context adapters and their settings, the Onyx version probe doctor's 'context:onyx' check uses, and the
    /// factory that builds the onyx source from the profile's onyx settings and Linear teams. The onyx source reads its
    /// key by calling <paramref name="environment"/> with the settings' pointer text (Sindri's Composition wraps the
    /// environment so pointers resolve there); it needs the host's TimeProvider and ILogger.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton(static _ => new OnyxVersionProbe(OnyxHttp.Client))
            .AddSingleton(sp => new OnyxSources(new OnyxSourceParts(
                OnyxHttp.Client, environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<OnyxClient>>())));
}
