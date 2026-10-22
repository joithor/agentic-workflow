using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Pr;

/// <summary>Registers the block runtimes of the pull request babysitter's Steps (PR babysitter spec §4). Their descriptors are registered with the blocks.</summary>
internal static class PrServiceCollectionExtensions
{
    /// <summary>Adds the pull request slices.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPrSlices(this IServiceCollection services) => services
        .AddBlockRuntime<PrReadyCheckStep>();
}
