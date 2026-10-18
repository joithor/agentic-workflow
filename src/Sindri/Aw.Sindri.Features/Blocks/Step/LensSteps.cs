using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>The lens frame shared by the design and review loops (line spec §5.2, Appendix A.3): descriptor only, agent runtime.</summary>
internal static class LensSteps
{
    /// <summary>
    /// 'step.lens.run': runs the queue's next lens through the fixed frame; 'reviewed' with 'fact:lensReport'. 'subject' ('design' or 'change') says what the frame reads,
    /// so the subject's own facts are not descriptor inputs; 'bundle' ('lenses') copies the pack's files into the Step directory.
    /// </summary>
    public static BlockDescriptor Run { get; } = AgentStep("step.lens.run", Capability.Tools)
        .Takes(new ParameterSpec("subject", ParameterType.Text, Required: true, IsCapabilityField: false), new ParameterSpec("bundle", ParameterType.Text, Required: false, IsCapabilityField: false))
        .Reads("fact:lensQueue", "fact:lensRounds").Reports("reviewed").Writes(On("fact:lensReport", "reviewed"));
}
