using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The design station's lens round and log Steps (line spec §5.2, Appendix C.1; LN-Y1): descriptors only. 'step.lens.run', the agent
/// Step between them, is in <see cref="LensSteps"/> (shared with the review loop).
/// </summary>
internal static class LensRoundSteps
{
    /// <summary>'step.lens.round': queues the pack's lenses for a round; 'ready'. 'pack' names the lens pack (a profile key).</summary>
    public static BlockDescriptor Round { get; } = BlockStep("step.lens.round", Capability.Tools)
        .Takes(new ParameterSpec("pack", ParameterType.Text, Required: false, IsCapabilityField: false))
        .Reads("fact:packs", "fact:lensRounds", "fact:changedSections").Reports("ready")
        .Writes(On("fact:lensQueue", "ready"));

    /// <summary>'step.lens.log': appends the report to the lens rounds; 'next' (a lens is still queued), 'clean' or 'findings'.</summary>
    public static BlockDescriptor Log { get; } = BlockStep("step.lens.log", Capability.None)
        .Reads("fact:lensReport", "fact:lensRounds", "fact:lensQueue").Reports("next", "clean", "findings")
        .Writes(Every("fact:lensRounds"), On("fact:lensQueue", "next"), On("fact:findings", "findings"));
}
