using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>
/// Registers the design and plan stations' block-runtime Steps (BS2). Like the scoping Steps they sit beside the composition, not in <c language="text">Blocks</c>,
/// because each joins a descriptor from <c language="text">Blocks.Step</c> with the code of <c language="text">Line</c>; the other design and plan blocks stay
/// descriptor-only in <c language="text">Blocks.BlockRegistrations</c>.
/// </summary>
internal static class LineBlockStepsServiceCollectionExtensions
{
    /// <summary>Adds each Step's descriptor and runtime (AddBlockStep), in Step id order.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddLineBlockSteps(this IServiceCollection services) => services
        .AddBlockStep<DesignCheckStep>()
        .AddBlockStep<PlanCheckStep>()
        .AddBlockStep<PlanQuestionsStep>()
        .AddBlockStep<PlanSettleStep>();
}
