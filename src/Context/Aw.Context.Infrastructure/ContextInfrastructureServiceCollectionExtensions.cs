using Aw.Context.Core.Docs;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Store;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the context store (context.db under $AW_SINDRI_HOME/context), git, the repo-docs index and source catalog,
    /// and the scrubber factory over Aw.Redaction (each call's scrubbers carry the profile's extra patterns). Needs a
    /// TimeProvider (the host registers it).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<ContextDatabase>()
            .AddSingleton<GitCli>()
            .AddSingleton<IScrubberFactory, RedactionScrubberFactory>()
            .AddSingleton<ISourceCatalog, RepoDocsCatalog>()
            .AddSingleton<IDocIndex, SqliteDocIndex>();
}
