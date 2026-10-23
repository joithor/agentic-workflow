using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.ListEngineInstances;
using Aw.Sindri.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListEngineInstances;

/// <summary>'GET /engine/instances?state=&amp;stuck=' (API spec §3.8, E-30, P13): what each instance waits on, its age against the clock, and when it counts as stuck (UI screens §4.6).</summary>
[Lane(TestLane.Unit)]
public sealed class ListEngineInstancesServiceTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeEngineReader _engine = new();

    private ListEngineInstancesService Service() => new(_engine, _clock);

    private async Task<EngineInstanceList> List(string? state = null, string? stuck = null)
    {
        var served = await Service().Handle(new ListEngineInstancesRequest(state, stuck), TestContext.Current.CancellationToken);
        Assert.True(served.IsSuccess, served.IsError ? served.Error.Message : string.Empty);
        return served.Value;
    }

    [Fact]
    public async Task An_instance_waiting_on_an_activity_carries_the_wait_its_age_the_retries_and_an_opaque_id()
    {
        _engine.Running.Add(EngineFacts.Instance("ITM-01JE9/item/1/step.4", EngineFacts.Wait(EngineWaitKinds.Activity, KnownInstants.Epoch.AddSeconds(-120), "step.checks"), retries: new EngineRetries(2, "exit 1")));

        var instance = Assert.Single((await List()).Instances);

        Assert.Equal(new EngineWaitingOn("activity", "step.checks", 120), instance.WaitingOn);
        Assert.Equal(new EngineRetries(2, "exit 1"), instance.Retries);
        Assert.Equal(("ITM-01JE9/item/1/step.4", "workflow.step", 88L, false), (instance.Address.Value, instance.Recipe.Id.Value, instance.HistoryLength, instance.Stuck));
        Assert.StartsWith("WFI-", instance.Id.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(299, false)]
    [InlineData(300, true)]
    [InlineData(3600, true)]
    public async Task A_signal_wait_is_stuck_from_five_minutes_on(int waitedSeconds, bool stuck)
    {
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddSeconds(-waitedSeconds))));

        var list = await List();

        Assert.Equal((stuck, stuck ? 1 : 0), (Assert.Single(list.Instances).Stuck, list.Pending.Stuck));
    }

    [Fact]
    public async Task A_running_timer_or_an_activity_wait_is_never_stuck_however_old()
    {
        var old = KnownInstants.Epoch.AddHours(-3);
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, old), timers: [new EngineTimer("produce-timeout", KnownInstants.Epoch.AddMinutes(5), "timeout")]));
        _engine.Running.Add(EngineFacts.Instance("T-2/step/1", EngineFacts.Wait(EngineWaitKinds.Activity, old, "step.checks")));
        _engine.Running.Add(EngineFacts.Instance("T-3/step/1", waiting: null));

        var list = await List();

        Assert.All(list.Instances, i => Assert.False(i.Stuck));
        Assert.Equal(0, list.Pending.Stuck);
    }

    [Fact]
    public async Task The_clock_decides_so_a_wait_becomes_stuck_when_time_passes_without_a_poll()
    {
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch)));
        Assert.Equal(0, (await List()).Pending.Stuck);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, (await List()).Pending.Stuck);
    }

    [Fact]
    public async Task The_filters_narrow_the_instances_but_pending_still_counts_every_running_one()
    {
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddMinutes(-10)), state: "produce"));
        _engine.Running.Add(EngineFacts.Instance("T-2/step/1", EngineFacts.Wait(EngineWaitKinds.Activity, KnownInstants.Epoch.AddMinutes(-1), "step.checks"), state: "checks"));

        Assert.Equal(["T-1/step/1"], (await List(stuck: "true")).Instances.Select(static i => i.Address.Value), StringComparer.Ordinal);
        Assert.Equal(["T-2/step/1"], (await List(stuck: "false")).Instances.Select(static i => i.Address.Value), StringComparer.Ordinal);
        var checks = await List(state: "checks");
        Assert.Equal(["T-2/step/1"], checks.Instances.Select(static i => i.Address.Value), StringComparer.Ordinal);
        Assert.Equal(new EnginePending(0, 1, 1, 0), checks.Pending);
    }

    [Fact]
    public async Task Instances_come_by_address()
    {
        _engine.Running.Add(EngineFacts.Instance("T-2/step/1", waiting: null));
        _engine.Running.Add(EngineFacts.Instance("T-1/step/1", waiting: null));

        Assert.Equal(["T-1/step/1", "T-2/step/1"], (await List()).Instances.Select(static i => i.Address.Value), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("TRUE1")]
    public async Task A_stuck_filter_that_is_not_true_or_false_is_SND_API_013(string stuck)
    {
        var served = await Service().Handle(new ListEngineInstancesRequest(Stuck: stuck), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.InvalidQuery.Code, served.Error.Code);
    }

    [Fact]
    public async Task A_server_that_does_not_answer_is_SND_ENGINE_001()
    {
        _engine.Down = true;

        var served = await Service().Handle(new ListEngineInstancesRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, served.Error.Code);
    }
}
