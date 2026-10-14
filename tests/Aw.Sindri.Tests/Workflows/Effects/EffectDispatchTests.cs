using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// Effects are activities (engine spec rev 8.1 §5.7; engine P6 unit (a)), on the time-skipping server: a transition's
/// effect writes 'outbox.pending' after the transition's ledger event, waits out 'workflow.dispatchDelay' (10 s by default)
/// on a workflow timer, then runs; a cancel within the delay aborts it; an engine notice skips the delay. The recheck is
/// unit (b)'s and 'effect.notify.desk' unit (c)'s: <see cref="PassingRecheck"/> and <see cref="FakeDeskEffect"/> stand in.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectDispatchTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _delay = EffectSettings.DefaultDispatchDelay;
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task An_effect_waits_out_the_dispatch_delay_then_runs_and_the_transition_lists_its_id()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await using var sindri = _harness.Sindri(_desk.Replace);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, PassingRecheck.Options(sindri));

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-71");
            await Armed(handle);
            var pending = Assert.Single(await EffectRows.Of(sindri, OutboxEvents.Pending, CommandOrigin.Recipe));
            await env.DelayAsync(_delay - TimeSpan.FromSeconds(1), Token);
            var early = await EffectRows.Of(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe);
            await env.DelayAsync(TimeSpan.FromSeconds(2), Token);
            var dispatched = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            var transition = await Transition(sindri, "sent");

            Assert.Equal((BlockId.From("effect.notify.desk"), handle.Id), (pending.Kind, pending.Instance.Value));
            Assert.Empty(early);
            Assert.Equal((pending.Identity, pending.Command), (dispatched.Identity, dispatched.Command));
            Assert.Equal([pending.Identity], _desk.Performed);
            Assert.Equal([pending.Identity], transition.Effects);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    [Fact]
    public async Task A_cancel_within_the_dispatch_delay_aborts_the_effect()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await using var sindri = _harness.Sindri(_desk.Replace);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, PassingRecheck.Options(sindri));

        await worker.ExecuteAsync(async () =>
        {
            var handle = await Completed(sindri, "T-72");
            await Armed(handle);
            var nonce = CommandNonce.From(new string('c', 32));
            var cancel = new CancelRequested("human:cli", CancelOrigin.Command, new CommandStamp(nonce, new string('0', 64)), Sequence: null, Parent: null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(nonce.Value));
            var ended = await handle.GetResultAsync();
            var refused = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Refused, CommandOrigin.Recipe));
            var pending = Assert.Single(await EffectRows.Of(sindri, OutboxEvents.Pending, CommandOrigin.Recipe));

            Assert.Equal("cancelled", ended.State);
            Assert.Equal((pending.Identity, EffectState.Refused, null), (refused.Identity, refused.State, refused.Code));
            Assert.StartsWith("cancelled", refused.Reason, StringComparison.Ordinal);
            Assert.Empty(await EffectRows.Of(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));
            Assert.Empty(_desk.Calls);
        }, Token);
    }

    /// <summary>
    /// The engine's own notice (here the first desk task's) is dispatched at once: no delay, no recheck, so a park
    /// interrupts promptly. Red until engine P6 unit (c) builds EngineNotices.For.
    /// </summary>
    [Fact]
    public async Task An_engine_notice_is_dispatched_without_the_delay()
    {
        var env = server.Environment;
        await using var sindri = _harness.Sindri(_desk.Replace);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, PassingRecheck.Options(sindri));

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-73", Token));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var dispatched = await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Engine);

            Assert.Equal(BlockId.From("effect.notify.desk"), Assert.Single(dispatched).Kind);
            Assert.DoesNotContain(await Timers(handle), static t => t == _delay);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>Starts 'workflow.dev-notify' on <paramref name="item"/> and completes its desk Step: 'ask -> sent' with 'effect.notify.desk'.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Completed(IServiceProvider sindri, string item)
    {
        var handle = await WorkflowsHarness.Start(server.Environment.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", item, Token));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
        var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
            new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = _harness.Fact("Hello.") }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
        var completed = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
        Assert.Equal("-> sent", completed.Happened);
        return handle;
    }

    /// <summary>Waits until the instance's history has the dispatch delay's timer (it follows the pending row's local activity).</summary>
    private static async Task Armed(WorkflowHandle handle)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if ((await Timers(handle)).Contains(_delay))
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail("the dispatch delay's timer was never started");
    }

    /// <summary>The durations of the timers the instance started.</summary>
    private static async Task<IReadOnlyList<TimeSpan>> Timers(WorkflowHandle handle) =>
        [.. (await handle.FetchHistoryAsync()).Events
            .Where(static e => e.EventType == EventType.TimerStarted)
            .Select(static e => e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan())];

    /// <summary>The 'workflow.transition' row that entered <paramref name="state"/>.</summary>
    private static async Task<WorkflowEventPayload> Transition(IServiceProvider sindri, string state)
    {
        var events = sindri.GetRequiredService<EventFactory>();
        return Assert.Single((await EffectRows.Events(sindri, EventTypes.WorkflowTransition))
            .Select(e => EffectRows.Payload<WorkflowEventPayload>(events, e)), p => p.To == state);
    }
}
