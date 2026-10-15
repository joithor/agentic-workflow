using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'RecordEvent' under a workflow-task replay (review run 2, data-integrity-guardian-r2-0): the same key with the same event
/// records once; the same key with a different event (the replay saw more signals) records under '{key}:{content hash}'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RecordEventReplayTests
{
    private const string Key = "workflow:T-1/step/1:run-1:event:3";

    [Fact(Timeout = 30_000)]
    public async Task The_same_event_under_the_same_key_records_once()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;

        var first = await activities.Record(Moved("checks"), Key, "event:3", ct);
        var again = await activities.Record(Moved("checks"), Key, "event:3", ct);

        Assert.Equal((false, true, first.Sequence), (first.AlreadyRecorded, again.AlreadyRecorded, again.Sequence));
        _ = Assert.Single((await ledger.ReadAfter(0, 100, ct)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_different_event_under_a_taken_key_records_under_the_key_and_a_content_hash()
    {
        var (activities, ledger) = await Open();
        var ct = TestContext.Current.CancellationToken;

        _ = await activities.Record(Moved("checks"), Key, "event:3", ct);
        var other = await activities.Record(Moved("produce") with { Trigger = "answer" }, Key, "event:3", ct);
        var rows = (await ledger.ReadAfter(0, 100, ct)).Value;

        Assert.False(other.AlreadyRecorded);
        Assert.Equal(2, rows.Count);
        Assert.Equal(Key, rows[0].IdempotencyKey);
        Assert.Matches("^" + Key + ":[0-9a-f]{12}$", rows[1].IdempotencyKey);
    }

    private static WorkflowEventRecord Moved(string to) =>
        new("T-1/step/1", EventTypes.WorkflowTransition, "T-1", "produce", to, "step.completed", "done", "1.1");

    private static async Task<(RecordEventActivities Activities, Core.Ledger.ILedgerStore Ledger)> Open()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var probe = new FakeProcessProbe();
        var store = new InMemorySindriStore(clock, probe);
        var held = new HeldLease();
        held.Hold((await store.Acquire(probe.Self(), TestContext.Current.CancellationToken)).Value);
        var ids = new SequentialIdGenerator();
        var events = new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids));
        return (new RecordEventActivities(store, held, events), store);
    }
}
