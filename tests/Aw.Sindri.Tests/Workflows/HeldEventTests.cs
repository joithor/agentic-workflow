using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'workflow.eventHold' (engine spec §5.4, §10 'Held_event_expires'): a delivered event that no wait has consumed within
/// the hold (default 24 h) is dropped with one 'workflow.expired' ledger event, and never applies afterwards.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HeldEventTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Held_event_expires()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-61", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");

            // 'claim.created' fits no wait in 'triaged', so it is held
            var held = Event("claim.created", 7, outcome: null) with { OccurredAt = RealTime.NowOffset };
            await handle.SignalAsync(wf => wf.EventAsync(held));
            await env.DelayAsync(TimeSpan.FromHours(25), ct);
            var rows = await WorkflowRows(sindri, static types => types.Contains("workflow.expired", StringComparer.Ordinal), ct);

            Assert.Equal(["workflow.started", "workflow.expired"], rows);
            var expired = System.Text.Json.JsonDocument.Parse((await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value.Single(static e => e.Envelope.Type == "workflow.expired").Envelope.Payload).RootElement.GetProperty("detail");
            Assert.Equal("service:workflow.events", expired.GetProperty("triggerActor").GetString());   // the expired event's own trigger
            Assert.EndsWith(":" + held.KeyPart, expired.GetProperty("triggerId").GetString(), StringComparison.Ordinal);
            Assert.Equal(7, expired.GetProperty("sourceSequence").GetInt64());
            Assert.Equal("triaged", (await WorkflowsHarness.Until(handle, static _ => true)).State);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_event_that_fits_the_wait_is_consumed_even_when_it_arrives_older_than_the_hold()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-62", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");

            // why: the bridge delivered it late (serve was down for a day); it fits the wait, so it satisfies it (§5.4: only a buffered signal expires)
            var late = Event("item.routed", 8, OutcomeName.From("notStartable")) with { OccurredAt = RealTime.NowOffset - TimeSpan.FromHours(25) };
            await handle.SignalAsync(wf => wf.EventAsync(late));
            var result = await handle.GetResultAsync();
            var rows = await WorkflowRows(sindri, static types => types.Count >= 2, ct);

            Assert.Equal(("dropped", "unresolved"), (result.State, result.Outcome));
            Assert.Equal(["workflow.started", "workflow.transition"], rows);
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_event_that_fits_no_wait_and_arrives_older_than_the_hold_is_dropped()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-63", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");

            var stale = Event("claim.created", 9, outcome: null) with { OccurredAt = RealTime.NowOffset - TimeSpan.FromHours(25) };
            await handle.SignalAsync(wf => wf.EventAsync(stale));
            var rows = await WorkflowRows(sindri, static types => types.Contains("workflow.expired", StringComparer.Ordinal), ct);

            Assert.Equal(["workflow.started", "workflow.expired"], rows);
            Assert.Equal("triaged", (await WorkflowsHarness.Until(handle, static _ => true)).State);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static WaitSatisfied Event(string kind, long sequence, OutcomeName? outcome) =>
        new("service:workflow.events", new EventWaitSource(EventKind.From(kind), sequence), outcome, new Dictionary<FactKey, FactValue>());

    /// <summary>The instance's workflow.* ledger types, read until <paramref name="holds"/> (the mirror follows the workflow task).</summary>
    private static async Task<IReadOnlyList<string>> WorkflowRows(IServiceProvider sindri, Func<IReadOnlyList<string>, bool> holds, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        IReadOnlyList<string> types = [];
        while (RealTime.NowDateTime < deadline)
        {
            types = [.. (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("workflow.", StringComparison.Ordinal))];
            if (holds(types))
            {
                return types;
            }

            await Task.Delay(100, ct);
        }

        return types;
    }
}
