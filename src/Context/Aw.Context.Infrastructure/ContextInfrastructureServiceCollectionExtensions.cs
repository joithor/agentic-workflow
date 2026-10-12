using Aw.Context.Core.Docs;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the context store (context.db under $AW_SINDRI_HOME/context), git, the repo-docs index and source catalog,
    /// both scrubbing ports over Aw.Redaction's built-in rules, the Onyx version probe doctor's 'context:onyx' check uses,
    /// and the factory that builds the onyx source from the profile's onyx settings and Linear teams. The onyx source
    /// reads its key by calling <paramref name="environment"/> with the settings' pointer text (Sindri's Composition
    /// wraps the environment so pointers resolve there). Needs a TimeProvider and ILogger (the host registers them).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<ContextDatabase>()
            .AddSingleton<GitCli>()
            .AddSingleton(new RedactionScrubber(Redactor.Default))
            .AddSingleton<IEgressScrubber>(static sp => sp.GetRequiredService<RedactionScrubber>())
            .AddSingleton<IIngestFilter>(static sp => sp.GetRequiredService<RedactionScrubber>())
            .AddSingleton<ISourceCatalog, ContextSourceCatalog>()
            .AddSingleton<IDocIndex, SqliteDocIndex>()
            .AddSingleton(static _ => new OnyxVersionProbe(OnyxHttp.Client))
            .AddSingleton(sp => new OnyxSources(new OnyxSourceParts(
                OnyxHttp.Client, environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<OnyxClient>>())));
}
