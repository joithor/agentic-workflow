using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The proof blocks' Steps (DT spec §4; line spec DTC6: they read 'fact:phasePacket' and 'fact:workspace'): descriptors only, agent runtime.
/// </summary>
internal static class ProofSteps
{
    /// <summary>'step.handtest.run': the hand test in the running app; 'works' or 'broken', 'fact:manualTest' on both.</summary>
    public static BlockDescriptor HandtestRun { get; } = AgentStep("step.handtest.run", Capability.Tools).Reads("fact:phasePacket", "fact:workspace").Reports("works", "broken").Writes(Every("fact:manualTest"));

    /// <summary>'step.verify.journey': the journeys in the running app; 'pass' or 'fail', 'fact:evidencePack' on both.</summary>
    public static BlockDescriptor VerifyJourney { get; } = AgentStep("step.verify.journey", Capability.Tools).Reads("fact:phasePacket", "fact:workspace").Reports("pass", "fail").Writes(Every("fact:evidencePack"));

    /// <summary>'step.verify.design': the design comparison; 'pass', 'fail' or 'skipped'; 'fact:designCheck' on 'pass' and 'fail'.</summary>
    public static BlockDescriptor VerifyDesign { get; } = AgentStep("step.verify.design", Capability.Tools).Reads("fact:phasePacket", "fact:workspace").Reports("pass", "fail", "skipped").Writes(On("fact:designCheck", "pass", "fail"));
}
