using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Engine;
using Aw.Sindri.Workflows;
using Xunit;

namespace Aw.Sindri.Tests.Workflows;

public sealed class ParkedRefusalTests
{
    public enum Case
    {
        ParkDesk,
        ParkBlock,
        TakeoverDesk,
        TakeoverBlock,
        TakeoverOverParkDesk,
        TakeoverOverParkBlock,
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(Case.ParkDesk, "answer --resume first, then complete run RUN")]
    [InlineData(Case.ParkBlock, "answer --resume first (the resume runs the Step again)")]
    [InlineData(Case.TakeoverDesk, "hand it back first: sindri workflow handback ADDR, then complete run RUN")]
    [InlineData(Case.TakeoverBlock, "hand it back first: sindri workflow handback ADDR (the handback runs the Step again)")]
    [InlineData(Case.TakeoverOverParkDesk, "hand it back first: sindri workflow handback ADDR, then answer --resume, then complete run RUN")]
    [InlineData(Case.TakeoverOverParkBlock, "hand it back first: sindri workflow handback ADDR, then answer --resume (the resume runs the Step again)")]
    public void ParkedRefusal_names_the_next_step_for_the_suspension_and_runtime(Case which, string tail)
    {
        var block = which is Case.ParkBlock or Case.TakeoverBlock or Case.TakeoverOverParkBlock;
        var instance = Build(which, block);
        var run = instance.Frame.Run!.Token;

        var text = RecipeWorkflow.ParkedRefusal(instance, instance.Suspension!, run);

        var address = instance.Address.Value;
        Assert.Equal($"{address} is parked ({ReasonOf(instance)}); " + tail.Replace("ADDR", address, StringComparison.Ordinal).Replace("RUN", run.Value, StringComparison.Ordinal), text);
    }

    private static string ReasonOf(WorkflowInstance instance) => instance.Suspension!.Reason?.ToString() ?? instance.Suspension.Kind.ToString();

    private static WorkflowInstance Build(Case which, bool block)
    {
        var producing = FrameFixture.Producing();
        var runtime = block ? StepRuntime.Block : StepRuntime.Desk;
        var from = producing with { Frame = producing.Frame with { Run = producing.Frame.Run! with { Runtime = runtime } } };
        var park = FrameFixture.Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Transition);
        return which switch
        {
            Case.ParkDesk or Case.ParkBlock => FrameFixture.Suspended(park, from),
            Case.TakeoverDesk or Case.TakeoverBlock => FrameFixture.Suspended(FrameFixture.Takeover(), from),
            _ => FrameFixture.Suspended(FrameFixture.Takeover(), FrameFixture.Suspended(park, from)),
        };
    }
}
