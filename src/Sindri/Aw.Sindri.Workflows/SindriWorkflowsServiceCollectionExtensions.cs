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
            .AddSingleton<RecordEventActivities>()
            .AddSingleton(provider => SindriWorker.Options(provider.GetRequiredService<RunStepActivities>()));
}
