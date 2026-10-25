using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The per-phase child's own Steps (line spec §7.1, App. C.6): descriptors only. 'seed' and 'drift' are block Steps, 'decide' is the
/// owner's, human-only. A served instance parks at each with SND-WF-010 until a runtime lands; 'recipe eval' needs none.
/// </summary>
internal static class PhaseSteps
{
    /// <summary>'step.phase.seed': checks the phase packet and its dependencies, prepares the worktree, and writes the round-1 value of every fact a later wait reads.</summary>
    public static BlockDescriptor Seed { get; } = BlockStep("step.phase.seed", Capability.Tools)
        .Reads("fact:phasePacket", "fact:lineContext")
        .Reports("seeded")
        .Writes(On("fact:buildNotes", "seeded"), On("fact:workspace", "seeded"), On("fact:built", "seeded"), On("fact:change", "seeded"), On("fact:redTests", "seeded"));

    /// <summary>'step.phase.drift': 'same' when review left the proven head unchanged, 'moved' with the new built head and change when it did not.</summary>
    public static BlockDescriptor Drift { get; } = BlockStep("step.phase.drift", Capability.Tools)
        .Reads("fact:built", "fact:change", "fact:workspace")
        .Reports("same", "moved")
        .Writes(On("fact:built", "moved"), On("fact:change", "moved"));

    /// <summary>'step.phase.decide': the owner rebuilds the phase with notes or stops it.</summary>
    public static BlockDescriptor Decide { get; } = HumanDeskStep("step.phase.decide", Capability.None)
        .Reads("fact:phasePacket")
        .Reviewing(ReviewOf("fact:phasePacket", DeskReviewKind.Packet))
        .Reports("rebuild", "stop")
        .Writes(On("fact:buildNotes", "rebuild"));
}
