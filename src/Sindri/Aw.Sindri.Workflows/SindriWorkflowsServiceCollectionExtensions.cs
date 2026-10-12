using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the activities, the worker options, the instance runtime and the stub item and approval ports.</summary>
public static class SindriWorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the dev server (the <see cref="ITemporalServer"/> port 'sindri serve' starts and stops), the activities, the
    /// worker's options, the <see cref="IInstanceRuntime"/> the workflow verbs use, and engine P4's stub
    /// <see cref="IApprovedRecipes"/> (until P2) and <see cref="IItems"/> (until API P3), all under the one Sindri home
    /// <see cref="TemporalSettings"/> reads.
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
            .AddSingleton<TemporalFrontend>()
            .AddSingleton<IApprovedRecipes, HomeApprovedRecipes>()   // stub until P2's registry
            .AddSingleton<IItems, SelfItems>()                       // stub until API P3's items
            .AddSingleton<IInstanceRuntime, TemporalInstanceRuntime>()
            .AddSingleton<IWorkflowInstances, TemporalWorkflowInstances>()
            .AddSingleton<ITemporalWorker, SindriWorkerHost>()
            .AddSingleton<IFactStore, HomeFactStore>()               // engine P4 unit (c): facts stored 0600 before the report
            .AddSingleton<ICommandNonces, HomeCommandNonces>()           // engine P4 unit (c): the nonce table (stand-in until API's IssueNonce)
            .AddSingleton<TemporalDeskReports>()                     // engine P4 unit (c): Send's 'complete' path
            .AddSingleton(new DeskSettings(DeskTimeouts.DefaultFloor));   // engine P4 unit (c): 'workflow.deskTimeout' until S1-D11 (unit (e))
    }
}
