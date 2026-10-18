using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// TEMPORARY stand-in for the Steps of sindri-engine's workflow.prove (line spec App. C.4), so workflow.phase validates and evals before
/// engine's recipe lands. Dropped, with its recipe, when engine's branch replaces it.
/// </summary>
internal static class StandInProveSteps
{
    /// <summary>The prove stand-in, after the three proof children: 'proven' with the proof pack, or 'incomplete'.</summary>
    public static BlockDescriptor Prove { get; } = AgentStep("step.standin.prove", Capability.Tools)
        .Reads("fact:phasePacket", "fact:built", "fact:workspace", "fact:redTests")
        .Reports("proven", "incomplete")
        .Writes(On("fact:proofPack", "proven"));

    /// <summary>The prove stand-in's failure log: 'logged' with the findings.</summary>
    public static BlockDescriptor ProveLog { get; } = AgentStep("step.standin.prove-log", Capability.Tools)
        .Reads("fact:phasePacket", "fact:built")
        .Reports("logged")
        .Writes(On("fact:findings", "logged"));
}
