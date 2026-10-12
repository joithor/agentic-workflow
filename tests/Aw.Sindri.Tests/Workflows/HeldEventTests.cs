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

    [Fact]
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
            var held = Event("claim.created", 7, outcome: null) with { OccurredAt = DateTimeOffset.UtcNow };
            await handle.SignalAsync(wf => wf.EventAsync(held));
            await env.DelayAsync(TimeSpan.FromHours(25), ct);
            var rows = await WorkflowRows(sindri, static types => types.Contains("workflow.expired", StringComparer.Ordinal), ct);

            Assert.Equal(["workflow.started", "workflow.expired"], rows);
            Assert.Equal("triaged", (await WorkflowsHarness.Until(handle, static _ => true)).State);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task An_event_older_than_the_hold_when_it_arrives_is_dropped_instead_of_moving_the_wait()
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

            // why: the bridge delivered it late (serve was down for a day); it fits the wait but is past the hold
            var stale = Event("item.routed", 8, OutcomeName.From("notStartable")) with { OccurredAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(25) };
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
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        IReadOnlyList<string> types = [];
        while (DateTime.UtcNow < deadline)
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
