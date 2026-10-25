using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Features.Blocks.Step;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the test-only blocks a proof drives; Composition calls it only when the prove gate is open.</summary>
public static class SindriProveFeaturesServiceCollectionExtensions
{
    /// <summary>Adds the test-only blocks: the input 'input.prove.echo' and the block Step 'step.prove.echo' (descriptors and runtimes).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriProveBlocks(this IServiceCollection services) => services.AddStepInput<ProveEchoInput>().AddBlockStep<ProveEchoStep>();
}
