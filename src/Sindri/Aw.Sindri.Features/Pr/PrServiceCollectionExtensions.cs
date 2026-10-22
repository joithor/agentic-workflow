using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Pr;

/// <summary>Registers the block runtimes of the PR babysitter's Steps (PB spec §10.1); their descriptors are registered with the blocks.</summary>
internal static class PrServiceCollectionExtensions
{
    /// <summary>Adds the PR Steps' runtimes by block id.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPrSlices(this IServiceCollection services) => services
        .AddBlockRuntime<PrCiLogStep>();
}
