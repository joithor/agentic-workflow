using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The review loop's Steps (DT spec §4.1; line spec DTC4): descriptors only. The lenses run through 'step.lens.run' (LensSteps). 'step.review.fix' writes a new 'fact:change', its head moved, so the loop reviews the fixed head.
/// </summary>
internal static class ReviewSteps
{
    /// <summary>'step.review.round': starts a review round; queues the review pack's lenses that apply to the changed paths, in pack order, and records the others as not applicable in the carried 'fact:lensRounds'. 'ready', or 'noLens' when no lens applies (the queue would be empty; the loop ends stuck for the owner).</summary>
    public static BlockDescriptor Round { get; } = BlockStep("step.review.round", Capability.None)
        .Reads("fact:change", "fact:phasePacket", "fact:lensRounds").Reports("ready", "noLens").Writes(On("fact:lensQueue", "ready"), On("fact:lensRounds", "ready"));

    /// <summary>
    /// 'step.review.log': appends the lens report to the round's log. 'next' while the queue is not empty; at the round's end 'clean' (zero blocking and zero issue),
    /// 'findings', or 'behaviourWrong' (a blocking finding that the behaviour is wrong), the running 'fact:review' on each of those three. 'needsOwner' instead of 'findings' when a
    /// blocking or issue finding carries a typed escalation (owner, 2026-10-19; 'fact:escalation' is its question): the only way the loop reaches a human.
    /// </summary>
    public static BlockDescriptor Log { get; } = BlockStep("step.review.log", Capability.None)
        .Reads("fact:lensReport", "fact:lensRounds", "fact:lensQueue").Reports("next", "clean", "findings", "behaviourWrong", "needsOwner")
        .Writes(Every("fact:lensRounds"), On("fact:lensQueue", "next"), On("fact:review", "clean", "findings", "behaviourWrong", "needsOwner"), On("fact:escalation", "needsOwner"));

    /// <summary>'step.review.record': writes the review result packet (rounds, each finding with its class and resolution, the last verdict, the head sha) and machine-freezes it; 'recorded' after a clean review, 'recordedWrong' after a behaviour-wrong one (line spec DTC4).</summary>
    public static BlockDescriptor Record { get; } = BlockStep("step.review.record", Capability.Tools)
        .Reads("fact:change", "fact:review", "fact:lensRounds", "fact:phasePacket").Reports("recorded", "recordedWrong").Writes(On("fact:review", "recorded", "recordedWrong"));

    /// <summary>
    /// 'step.review.fix': fixes the findings; 'fixed' with the new 'fact:change'. 'needsOwner' with a typed 'fact:escalation' when the fix is ambiguous or needs a product decision
    /// (owner, 2026-10-19): the fixer's one way to a human.
    /// </summary>
    public static BlockDescriptor Fix { get; } = AgentStep("step.review.fix", Capability.Tools | Capability.WriteScope).Reads("fact:change", "fact:review")
        .Reports("fixed", "needsOwner").Writes(On("fact:change", "fixed"), On("fact:escalation", "needsOwner"));

    /// <summary>
    /// 'step.review.apply-answer': after an escalation, fixes the findings the way the owner's 'fact:answers' say; 'fixed' with the new 'fact:change'. 'needsOwner' with a new typed
    /// 'fact:escalation' when the answer leaves another question (the loop caps the asks).
    /// </summary>
    public static BlockDescriptor ApplyAnswer { get; } = AgentStep("step.review.apply-answer", Capability.Tools | Capability.WriteScope).Reads("fact:change", "fact:review", "fact:escalation", "fact:answers")
        .Reports("fixed", "needsOwner").Writes(On("fact:change", "fixed"), On("fact:escalation", "needsOwner"));

    /// <summary>'step.review.recheck': reviews the fixed head; as 'step.lens.run' does per lens, reporting the round's verdict.</summary>
    public static BlockDescriptor Recheck { get; } = AgentStep("step.review.recheck", Capability.Tools).Reads("fact:change", "fact:review").Reports("clean", "findings", "behaviourWrong").Writes(Every("fact:review"));
}
