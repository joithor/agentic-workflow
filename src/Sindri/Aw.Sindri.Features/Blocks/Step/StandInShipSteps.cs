using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// TEMPORARY stand-in for the Step of sindri-engine's workflow.ship (line spec App. C.5), so workflow.phase validates and evals before
/// engine's recipe lands. Dropped, with its recipe, when engine's branch replaces it.
/// </summary>
internal static class StandInShipSteps
{
    /// <summary>The ship stand-in: 'shipped' with the pull request and hand-off, 'returned' with why, or 'refused'.</summary>
    public static BlockDescriptor Ship { get; } = AgentStep("step.standin.ship", Capability.Tools)
        .Reads("fact:phasePacket", "fact:proofPack", "fact:review", "fact:workspace", "fact:lineContext")
        .Reports("shipped", "returned", "refused")
        .Writes(On("fact:pr", "shipped"), On("fact:handoff", "shipped"), On("fact:returnNotes", "returned"));
}
