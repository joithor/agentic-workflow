using Aw.Context.Core.Docs;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Store;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the context store (context.db under $AW_SINDRI_HOME/context), git, the repo-docs index and source catalog,
    /// the scrubber factory over Aw.Redaction (each call's scrubbers carry the profile's extra patterns), the graph store,
    /// the map file reader and the exports folder. Needs a TimeProvider and the host's AwJson options (a
    /// JsonSerializerOptions singleton whose context holds the map types), both registered by the host.
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
            .AddSingleton<IDocIndex, SqliteDocIndex>()
            .AddSingleton<IGraphStore, SqliteGraphStore>()
            .AddSingleton<IMapFileReader, MapFileReader>()
            .AddSingleton<IExportStore, FileExportStore>();
}
