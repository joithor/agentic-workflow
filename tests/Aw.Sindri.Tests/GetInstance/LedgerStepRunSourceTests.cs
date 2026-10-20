using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The ledger-backed source keeps one instance's workflow events, in ledger order, and the fold over them is the same however many times the ledger is read.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerStepRunSourceTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/step/1");

    [Fact]
    public async Task Reads_only_the_instances_own_workflow_rows_in_order_and_folds_the_same_every_time()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var started = StepRunFixtures.Started("produce", "1.1").Payload;
        var done = StepRunFixtures.Completed("produce", "checks", "1.1", "done", tokens: 900).Payload;
        (string Type, string Key, string Item, WorkflowEventPayload Payload)[] rows =
        [
            ("workflow.started", "workflow:T-1/step/1:run-a:1", "T-1", started),
            ("workflow.started", "workflow:T-2/step/1:run-b:1", "T-2", started),
            ("workflow.started", "workflow:T-1/step/1/step.2:run-c:1", "T-1", started),
            ("workflow.transition", "workflow:T-1/step/1:run-a:3", "T-1", done),
        ];
        foreach (var (type, key, item, payload) in rows)
        {
            Assert.False((await h.Store.Append(h.Events.Entry(type, payload, key) with { Item = item }, grant, ct)).IsError);
        }

        var source = new LedgerStepRunSource(h.Store, h.Events);
        var first = await source.EventsOf(_address, ct);
        var second = await source.EventsOf(_address, ct);

        Assert.Equal("workflow.started workflow.transition", string.Join(' ', first.Value.Select(static e => e.Type)));
        var runs = StepRuns.Fold(first.Value, []);
        Assert.Equal(runs, StepRuns.Fold(second.Value, []));
        Assert.Equal(("1.1", StepRunState.Passed, 900), (runs[0].Token, runs[0].State, runs[0].Tokens));
    }
}
