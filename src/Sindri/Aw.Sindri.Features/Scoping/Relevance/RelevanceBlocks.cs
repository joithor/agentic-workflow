using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.Blocks.Step;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Scoping.Relevance;

/// <summary>Registers the relevance review's three Steps: their descriptors, the block runtimes of the judge and the apply Step, and the review agent's instructions.</summary>
internal static class RelevanceBlocks
{
    /// <summary>Adds the descriptors, the runtimes and the instructions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopeRelevance(this IServiceCollection services) => services
        .AddBlockDescriptor(RelevanceSteps.Review)
        .AddBlockDescriptor(RelevanceSteps.Judge)
        .AddBlockDescriptor(RelevanceSteps.Apply)
        .AddBlockRuntime<RelevanceJudgeStep>()
        .AddBlockRuntime<RelevanceApplyStep>()
        .AddSingleton<IAgentStepInstructions, RelevanceReviewInstructions>();
}
