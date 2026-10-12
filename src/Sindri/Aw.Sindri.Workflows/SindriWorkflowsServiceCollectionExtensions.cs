using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the RunStep activities, the worker options and the desk completion port.</summary>
public static class SindriWorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the dev server (the <see cref="ITemporalServer"/> port 'sindri serve' starts and stops), the activities, the
    /// worker's options and the desk completion port ('sindri workflow complete'; engine P0 spike), all under the one
    /// Sindri home <see cref="TemporalSettings"/> reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (the Sindri home and the state root).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = TemporalSettings.FromEnvironment(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(new WorkflowsHome(settings.Home))
            .AddSingleton<TemporalDevServer>()
            .AddSingleton<ITemporalServer, DevServerTemporal>()
            .AddSingleton<RunStepActivities>()
            .AddSingleton<RecordEventActivities>()
            .AddSingleton(provider => SindriWorker.Options(provider.GetRequiredService<RunStepActivities>(), provider.GetRequiredService<RecordEventActivities>()))
            .AddSingleton<TaskTokens>()
            .AddSingleton<TemporalFrontend>()
            .AddSingleton<IDeskSteps, TemporalDeskSteps>()
            .AddSingleton<IWorkflowInstances, TemporalWorkflowInstances>()
            .AddSingleton<ITemporalWorker, SindriWorkerHost>();
    }
}
