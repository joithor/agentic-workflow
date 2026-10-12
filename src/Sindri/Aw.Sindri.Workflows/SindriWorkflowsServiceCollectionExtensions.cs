using Aw.Sindri.Core.Control;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the RunStep activities and the worker options.</summary>
public static class SindriWorkflowsServiceCollectionExtensions
{
    /// <summary>Adds the dev server (the <see cref="ITemporalServer"/> port 'sindri serve' starts and stops), the activities and the worker's options.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (the Sindri home and the state root).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(TemporalSettings.FromEnvironment(environment))
            .AddSingleton<TemporalDevServer>()
            .AddSingleton<ITemporalServer, DevServerTemporal>()
            .AddSingleton<RunStepActivities>()
            .AddSingleton(provider => SindriWorker.Options(provider.GetRequiredService<RunStepActivities>()));
}
