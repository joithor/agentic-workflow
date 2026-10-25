using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The Step contract's three Steps (spec §12): descriptors only, desk-capable, so with no IStep runtime registered they
/// run on the desk runtime (W5, W20). 'step.produce' names the agent runtime (engine P8 Q4) and falls back to the desk without an agent target (D7).
/// </summary>
internal static class DeskSteps
{
    /// <summary>'step.produce': outcome 'done', with the required output 'fact:output'.</summary>
    public static BlockDescriptor Produce { get; } = Desk(
        "step.produce", inputs: [], outcomes: ["done"], outputs: [Output("fact:output", requiredOn: "done")], BlockRuntime.Agent);

    /// <summary>'step.checks': outcomes 'passed' and 'failed', with 'fact:findings' required on 'failed'.</summary>
    public static BlockDescriptor Checks { get; } = Desk(
        "step.checks", inputs: ["fact:output"], outcomes: ["passed", "failed"], outputs: [Output("fact:findings", requiredOn: "failed")]);

    /// <summary>'step.verify': outcomes 'accepted' and 'rejected', with 'fact:findings' required on 'rejected'.</summary>
    public static BlockDescriptor Verify { get; } = Desk(
        "step.verify", inputs: ["fact:output"], outcomes: ["accepted", "rejected"], outputs: [Output("fact:findings", requiredOn: "rejected")]);

    private static BlockDescriptor Desk(string id, string[] inputs, string[] outcomes, OutputSpec[] outputs, BlockRuntime? runtime = null) => new(
        BlockId.From(id), BlockKind.Step, Version: 1,
        Inputs: [.. inputs.Select(FactKey.From)],
        Outputs: outputs,
        Outcomes: [.. outcomes.Select(OutcomeName.From)],
        Capability.None, Parameters: [], Rung: null, DeskCapable: true, Runtime: runtime);

    private static OutputSpec Output(string fact, string requiredOn) => new(FactKey.From(fact), [OutcomeName.From(requiredOn)]);
}
