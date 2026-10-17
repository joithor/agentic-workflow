using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The review loop's Steps (DT spec §4.1; line spec DTC4 interface): descriptors only, agent runtime. 'step.review.fix' writes a new 'fact:change', its head moved, so the loop reviews the fixed head.
/// </summary>
internal static class ReviewSteps
{
    /// <summary>'step.review.run': 'clean', 'findings' or 'behaviourWrong', 'fact:review' on every one.</summary>
    public static BlockDescriptor Run { get; } = AgentStep("step.review.run", Capability.Tools).Reads("fact:change", "fact:phasePacket").Reports("clean", "findings", "behaviourWrong").Writes(Every("fact:review"));

    /// <summary>'step.review.fix': fixes the findings; 'fixed' with the new 'fact:change'.</summary>
    public static BlockDescriptor Fix { get; } = AgentStep("step.review.fix", Capability.Tools | Capability.WriteScope).Reads("fact:change", "fact:review").Reports("fixed").Writes(On("fact:change", "fixed"));

    /// <summary>'step.review.recheck': reviews the fixed head; as 'step.review.run'.</summary>
    public static BlockDescriptor Recheck { get; } = AgentStep("step.review.recheck", Capability.Tools).Reads("fact:change", "fact:review").Reports("clean", "findings", "behaviourWrong").Writes(Every("fact:review"));
}
