using Aw.Sindri.Core.Workflow.Engine.Desk;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the RunStep activities and the worker options.</summary>
public static class SindriWorkflowsServiceCollectionExtensions
{
    /// <summary>Adds the dev server, the activities and the worker's options. The host calls it once it hosts the worker (engine P4).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services)
        => services
            .AddSingleton<TemporalDevServer>()
            .AddSingleton<RunStepActivities>()
            .AddSingleton(provider => SindriWorker.Options(provider.GetRequiredService<RunStepActivities>()));

    /// <summary>
    /// Adds <see cref="AddSindriWorkflows(IServiceCollection)"/> and the desk completion port with the home read from
    /// <paramref name="environment"/> (engine P0 spike: 'sindri workflow complete').
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The environment ('AW_SINDRI_HOME').</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSindriWorkflows()
            .AddSingleton(WorkflowsHome.From(environment))
            .AddSingleton<TaskTokens>()
            .AddSingleton<IDeskSteps, TemporalDeskSteps>();
}
