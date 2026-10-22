using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.GetEngine;
using Aw.Sindri.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetEngine;

/// <summary>'GET /engine' (API spec §3.8, E-30, P13): the server, worker and mirror as the reader reports them, with the pending counts over the running instances.</summary>
[Lane(TestLane.Unit)]
public sealed class GetEngineServiceTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeEngineReader _engine = new();

    private GetEngineService Service() => new(_engine, _clock);

    [Fact]
    public async Task The_view_carries_the_server_the_worker_and_the_mirror_as_read_stamped_with_the_clock()
    {
        var served = await Service().Handle(new GetEngineRequest(), TestContext.Current.CancellationToken);

        Assert.True(served.IsSuccess);
        Assert.Equal(("temporal-dev", "sindri", "http://127.0.0.1:8233"), (served.Value.Server.Kind, served.Value.Server.Namespace, served.Value.Server.UiUrl));
        Assert.Equal((1, true, "sindri-48211", 0L), (served.Value.Worker.Count, served.Value.Worker.Polling, served.Value.Worker.Identity, served.Value.Worker.Backlog));
        Assert.Equal(new EngineMirror(0, 0), served.Value.Mirror);
        Assert.Equal(KnownInstants.Epoch, served.Value.AsOf);
    }

    [Fact]
    public async Task Pending_counts_the_signals_updates_and_stuck_instances_over_every_running_instance()
    {
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddMinutes(-6))));   // stuck
        _engine.Running.Add(EngineFacts.Instance("T-2/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddMinutes(-1))));   // waiting, not yet stuck
        _engine.Running.Add(EngineFacts.Instance("T-3/step/1", EngineFacts.Wait(EngineWaitKinds.Update, KnownInstants.Epoch.AddMinutes(-9))));   // stuck
        _engine.Running.Add(EngineFacts.Instance("T-4/step/1", EngineFacts.Wait(EngineWaitKinds.Activity, KnownInstants.Epoch.AddMinutes(-30), "step.checks")));

        var served = await Service().Handle(new GetEngineRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(new EnginePending(Updates: 1, Signals: 2, Stuck: 2, ContinueAsNew: 0), served.Value.Pending);
        Assert.Equal(1, served.Value.Worker.ActivitiesRunning);   // counted from the instances that wait on an activity, not taken from the worker's facts
    }

    [Fact]
    public async Task A_server_that_does_not_answer_is_SND_ENGINE_001()
    {
        _engine.Down = true;

        var served = await Service().Handle(new GetEngineRequest(), TestContext.Current.CancellationToken);

        Assert.True(served.IsError);
        Assert.Equal(SindriErrors.EngineUnavailable.Code, served.Error.Code);
    }
}
