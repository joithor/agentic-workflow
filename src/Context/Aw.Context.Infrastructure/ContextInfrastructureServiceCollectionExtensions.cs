using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Infrastructure;

/// <summary>Registers Aw.Context.Infrastructure's adapters and settings. A second technology for a port gets its own AddContext&lt;Tech&gt;Infrastructure method.</summary>
public static class ContextInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Context adapters and their settings: the graph store (context.db), the map file reader and the exports
    /// folder. The reader and the exports folder take the host's AwJson options (a JsonSerializerOptions singleton whose
    /// context holds the map types), which Composition registers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<GraphDatabase>()
            .AddSingleton<IGraphStore, SqliteGraphStore>()
            .AddSingleton<IMapFileReader, MapFileReader>()
            .AddSingleton<IExportStore, FileExportStore>();
}
