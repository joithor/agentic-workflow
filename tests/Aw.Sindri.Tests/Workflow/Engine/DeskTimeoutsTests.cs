using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The desk runtime's timeout (engine spec W20, §3.4, §3.8; rev 8.1 §5.3): the wait is at least 'workflow.deskTimeout'
/// (2d by default), and its expiry parks with 'timeout' on the same run, never a retry. Lane decision (engine P4 unit
/// (c)): W20 and §3.8 win over §5.3's 'step.failed(timeout)'.
/// </summary>
public sealed class DeskTimeoutsTests
{
    private const string Timers = "service:workflow.timers";

    [Fact, Trait("Lane", "unit")]
    public void Desk_wait_is_the_state_timeout_raised_to_the_desk_floor()
    {
        var floor = DeskTimeouts.DefaultFloor;

        Assert.Equal(TimeSpan.FromDays(2), floor);
        Assert.Equal(floor, DeskTimeouts.Wait(Step.States["produce"].Timeout, floor));   // 4h
        Assert.Equal(TimeSpan.FromHours(4), DeskTimeouts.Wait(Step.States["produce"].Timeout, TimeSpan.FromMinutes(1)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_timeout_never_retries()
    {
        var checks = At(Step, "checks", entry: 2);   // timeout: { after: 30m, then: retry }

        var expired = DeskTimeouts.Expire(checks, TimeSpan.FromDays(2), Timers);

        var park = Assert.IsType<Suspension>(expired.Suspension);
        Assert.Equal((SuspensionKind.Park, ParkReason.Timeout, ResumeKind.Rearm), (park.Kind, park.Reason, park.Resume.Kind));
        Assert.Equal("checks ran past 2d", park.Question);
        Assert.Equal(Timers, park.Actor);
        Assert.Equal(("parked", "checks", "2.1"), (expired.State, expired.Frame.State, expired.Frame.Run?.Token.Value));
        Assert.Same(expired, DeskTimeouts.Expire(expired, TimeSpan.FromDays(2), Timers));
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_timeout_names_how_long_the_state_waited()
    {
        var verify = At(Step, "verify", entry: 3);

        var expired = DeskTimeouts.Expire(verify, TimeSpan.FromHours(49), Timers);

        Assert.Equal((ParkReason.Timeout, "verify ran past 2d 1h"), (expired.Park, expired.Suspension?.Question));
    }
}
