using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the test-only blocks a proof drives; Composition calls it only when the prove gate is open.</summary>
public static class SindriProveFeaturesServiceCollectionExtensions
{
    /// <summary>Adds 'input.prove.echo' (descriptor and runtime).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriProveBlocks(this IServiceCollection services) => services.AddStepInput<ProveEchoInput>();
}
