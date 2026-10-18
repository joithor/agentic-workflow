using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflow.Engine;

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
        var run = RunToken.For(3, 1);
        var suspension = Assert.IsType<Suspension>(instance.Suspension);

        var text = RecipeWorkflow.ParkedRefusal(instance, suspension, run);

        var address = instance.Address.Value;
        Assert.Equal($"{address} is parked ({suspension.Reason?.ToString() ?? suspension.Kind.ToString()}); " + tail.Replace("ADDR", address, StringComparison.Ordinal).Replace("RUN", run.Value, StringComparison.Ordinal), text);
    }

    private static WorkflowInstance Build(Case which, bool block)
    {
        var producing = FrameFixture.Producing();
        var runtime = block ? StepRuntime.Block : StepRuntime.Desk;
        var from = producing with { Frame = producing.Frame with { Run = new OpenRun(RunToken.For(3, 1), runtime, Launch: null, Task: null) } };
        var park = FrameFixture.Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Transition);
        return which switch
        {
            Case.ParkDesk or Case.ParkBlock => FrameFixture.Suspended(park, from),
            Case.TakeoverDesk or Case.TakeoverBlock => FrameFixture.Suspended(FrameFixture.Takeover(), from),
            Case.TakeoverOverParkDesk or Case.TakeoverOverParkBlock => FrameFixture.Suspended(FrameFixture.Takeover(), FrameFixture.Suspended(park, from)),
            _ => throw new ArgumentOutOfRangeException(nameof(which)),
        };
    }
}
