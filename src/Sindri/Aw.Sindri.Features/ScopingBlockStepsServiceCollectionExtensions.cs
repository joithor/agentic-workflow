using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>
/// Registers the scoping station's block-runtime Steps (BS-1). They sit beside the composition, not in <c language="text">Blocks</c> or <c language="text">Scoping</c>, because
/// each joins a descriptor from the first with the code of the second (slices never name each other); the other scoping blocks stay descriptor-only in
/// <c language="text">Blocks.BlockRegistrations</c>, and the Steps listed here are the ones <c language="text">BlockStepRuntimes</c> holds.
/// </summary>
internal static class ScopingBlockStepsServiceCollectionExtensions
{
    /// <summary>Adds each Step's descriptor and runtime (AddBlockStep), in Step id order.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopingBlockSteps(this IServiceCollection services) => services
        .AddBlockStep<BriefSeedStep>()
        .AddBlockStep<InterviewRecordStep>()
        .AddBlockStep<ChallengeLogStep>()
        .AddBlockStep<ScopeAnchorStep>()
        .AddBlockStep<ScopeCheckStep>()
        .AddBlockStep<ScopeGatherStep>()
        .AddBlockStep<ScopeIntakeStep>()
        .AddBlockStep<ScopeProposeStep>()
        .AddBlockStep<ScopeReadBriefStep>()
        .AddBlockStep<ScopeReanchorStep>()
        .AddBlockStep<ScopeWithdrawStep>();

    /// <summary>Adds what the Steps reach the context engine through: its text shape and the run's proposals on the map. Registered beside the engine, not with the Steps, so a harness that has no engine can supply fakes.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopingBlockPorts(this IServiceCollection services) => services
        .AddScoped<IScopingTextShape, ScopingTextShape>()
        .AddScoped<IScopeRunProposals, ScopeRunProposals>();
}
