using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The human-only rule (DT spec §8.5, scoping-station spec W3): a human-only Step on the desk. A block-runtime Step on the desk is answerable by any caller (owner q93, 2026-10-23).</summary>
public sealed class HumanOnlyStepsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(true, null, StepRuntime.Desk, true)]
    [InlineData(true, null, StepRuntime.Agent, false)]
    [InlineData(true, BlockRuntime.Block, StepRuntime.Desk, true)]
    [InlineData(null, BlockRuntime.Block, StepRuntime.Desk, false)]
    [InlineData(null, BlockRuntime.Block, StepRuntime.Agent, false)]
    [InlineData(null, null, StepRuntime.Desk, false)]
    public void Only_human_only_steps_are_human_only_on_the_desk(bool? humanOnly, BlockRuntime? runtime, StepRuntime runOn, bool expected)
    {
        var step = new BlockDescriptor(
            BlockId.From("step.probe"), BlockKind.Step, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None, Parameters: [], Rung: null,
            DeskCapable: true, HumanOnly: humanOnly, Runtime: runtime);

        Assert.Equal(expected, HumanOnlySteps.Applies(step, runOn));
    }
}
