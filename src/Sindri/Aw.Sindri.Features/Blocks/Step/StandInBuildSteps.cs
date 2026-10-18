using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// TEMPORARY stand-in for the Step of sindri-engine's workflow.build (line spec App. C.3), so workflow.phase validates and evals before
/// engine's recipe lands. Dropped, with its recipe, when engine's branch replaces it.
/// </summary>
internal static class StandInBuildSteps
{
    /// <summary>The build stand-in: 'built' with the four facts the phase reads, or 'stopped'.</summary>
    public static BlockDescriptor Build { get; } = AgentStep("step.standin.build", Capability.Tools | Capability.WriteScope)
        .Reads("fact:phasePacket", "fact:workspace", "fact:buildNotes", "fact:lineContext")
        .Reports("built", "stopped")
        .Writes(On("fact:built", "built"), On("fact:change", "built"), On("fact:redTests", "built"), On("fact:activePacket", "built"));
}
