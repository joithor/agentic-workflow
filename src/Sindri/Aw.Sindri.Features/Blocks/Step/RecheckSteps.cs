using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>The re-proof child's Steps (PB spec §8, C.3): descriptors only.</summary>
internal static class RecheckSteps
{
    /// <summary>'step.recheck.plan': lists the journeys and evidence scripts to re-run on the head; 'planned' or 'nothing'.</summary>
    public static BlockDescriptor Plan { get; } = BlockStep("step.recheck.plan", Capability.Tools).Takes(Text("handoff"), Text("at"), Text("workspace"))
        .Reports("planned", "nothing").Writes(On("fact:recheckPlan", "planned"));

    /// <summary>'step.prove.rerun-scripts': re-runs the recorded evidence scripts on the head; 'pass' or 'fail'.</summary>
    public static BlockDescriptor RerunScripts { get; } = BlockStep("step.prove.rerun-scripts", Capability.Tools).Reads("fact:recheckPlan")
        .Reports("pass", "fail").Writes(Every("fact:findings"));

    /// <summary>'step.recheck.log': records the verdict: 'held', 'broken' or 'nothing'.</summary>
    public static BlockDescriptor Log { get; } = BlockStep("step.recheck.log", Capability.Tools).Reports("held", "broken", "nothing").Writes(Every("fact:recheck"));
}
