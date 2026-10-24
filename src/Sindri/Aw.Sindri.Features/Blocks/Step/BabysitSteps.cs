using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The Step of the 'workflow.pr-babysitter' STUB (plan Q3): one human-only Step that stands for babysitting, docs sync and the owner's
/// merge until the PR babysitter spec's recipe replaces the stub. Descriptor only.
/// </summary>
internal static class BabysitSteps
{
    /// <summary>'step.babysit.watch': the human says 'merged' (with the merged sha), 'rework' (with why it goes back to build) or 'stuck'.</summary>
    public static BlockDescriptor Watch { get; } = HumanDeskStep("step.babysit.watch", Capability.None)
        .Reads("fact:pr", "fact:handoff", "fact:phasePacket", "fact:lineContext", "fact:workspace")
        .Reviewing(ReviewOf("fact:prStatus", DeskReviewKind.Pr))
        .Reports("merged", "rework", "stuck")
        .Writes(On("fact:mergedSha", "merged"), On("fact:reworkNotes", "rework"));
}
