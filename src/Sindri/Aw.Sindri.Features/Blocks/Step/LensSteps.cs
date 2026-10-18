using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The lens frame's Steps (line spec §5.2, Appendix A.3, C.1; LN-Y1): one queue of lenses a round, one agent run a lens, one log a run.
/// Shared by the design station and, with 'subject: change', the review loop (DTC4). Descriptors only. The run reads the queue and
/// the earlier rounds; the subject itself is named by its 'subject' parameter, so the frame holds for either caller.
/// </summary>
internal static class LensSteps
{
    /// <summary>'step.lens.round': queues the pack's lenses for a round; 'ready'. 'pack' names the lens pack (a profile key).</summary>
    public static BlockDescriptor Round { get; } = BlockStep("step.lens.round", Capability.Tools)
        .Reads("fact:packs", "fact:lensRounds", "fact:changedSections").Reports("ready")
        .Writes(On("fact:lensQueue", "ready")) with { Parameters = [new ParameterSpec("pack", ParameterType.Text, Required: false, IsCapabilityField: false)] };

    /// <summary>'step.lens.run': one lens over the subject; 'reviewed' with the lens report. 'subject' is 'design' or 'change'; 'bundle' names the lens bundle.</summary>
    // TODO(W9/L-17): read-only capability (no descriptor field yet; the launch enforces it)
    public static BlockDescriptor Run { get; } = AgentStep("step.lens.run", Capability.Tools)
        .Reads("fact:lensQueue", "fact:lensRounds").Reports("reviewed")
        .Writes(On("fact:lensReport", "reviewed")) with { Parameters = [new ParameterSpec("subject", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("bundle", ParameterType.Text, Required: false, IsCapabilityField: false)] };

    /// <summary>'step.lens.log': appends the report to the lens rounds; 'next' (a lens is still queued), 'clean' or 'findings'.</summary>
    public static BlockDescriptor Log { get; } = BlockStep("step.lens.log", Capability.None)
        .Reads("fact:lensReport", "fact:lensRounds", "fact:lensQueue").Reports("next", "clean", "findings")
        .Writes(Every("fact:lensRounds"), On("fact:lensQueue", "next"), On("fact:findings", "findings"));
}
