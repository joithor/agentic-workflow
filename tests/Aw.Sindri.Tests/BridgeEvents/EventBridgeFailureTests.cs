using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.BridgeEvents;

/// <summary>
/// What the bridge does with an event it can't deliver (engine spec §5.4): one whose item isn't an item key, or whose
/// delivery fails for good, is logged and passed, so it can't stall every event behind it; a failure that may pass
/// (Temporal unavailable, a timeout) keeps the cursor before the event, so the next run retries it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EventBridgeFailureTests : IDisposable
{
    private const string Router = "router.stub";
    private readonly WorkflowsHarness _harness = new();
    private int _events;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task An_event_whose_item_is_not_an_item_key_is_skipped_and_the_cursor_moves_past_it()
    {
        BridgeRecipes.WriteTo(_harness);
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var sequence = await Append(sindri, "T-1\\");   // why: a visibility query treats the backslash as an escape and refuses it

        await using var scope = sindri.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == EventBridge.ServiceId);
        var ran = await bridge.Run(new ScheduledRun(sindri.GetRequiredService<HeldLease>().Current!, KnownInstants.Epoch), Token);

        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        Assert.Equal(sequence, (await sindri.GetRequiredService<EventBridgeCursor>().Read(Token)).Value);
    }

    [Fact]
    public async Task A_delivery_refused_for_good_is_passed_and_the_cursor_moves()
    {
        var (bridge, sequence, cursor) = await Prepared(SindriErrors.InvalidInput.With("refused"));

        var ran = await bridge.Run(Token);

        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
        Assert.Equal(sequence, (await cursor.Read(Token)).Value);
    }

    [Theory]
    [InlineData(ErrorKind.Unavailable)]
    [InlineData(ErrorKind.Timeout)]
    public async Task A_delivery_that_may_pass_keeps_the_cursor_before_the_event(ErrorKind kind)
    {
        var (bridge, _, cursor) = await Prepared(new Error("SND-TEST-001", kind, "try again"));

        var ran = await bridge.Run(Token);

        Assert.True(ran.IsError);
        Assert.Equal(0, (await cursor.Read(Token)).Value);
    }

    [Fact]
    public async Task A_run_that_signals_nothing_is_quiet_so_the_scheduler_records_no_row_after_an_ok_one()
    {
        var (bridge, _, _) = await Prepared(SindriErrors.InvalidInput.With("refused"));

        var first = await bridge.Run(Token);
        var idle = await bridge.Run(Token);

        Assert.Equal((ScheduledResult.Quiet, ScheduledResult.Quiet), (first.Value, idle.Value));
    }

    [Fact]
    public async Task A_run_that_signals_an_instance_worked()
    {
        var (bridge, _, _) = await Prepared(Unit.Value);

        var ran = await bridge.Run(Token);

        Assert.Equal(ScheduledResult.Worked, ran.Value);
    }

    /// <summary>A bridge over the real ledger whose live instance of 'T-47' answers a signal with the given errors (the last repeats), and its event.</summary>
    private async Task<(Bridge Bridge, long Sequence, EventBridgeCursor Cursor)> Prepared(params Result<Unit>[] answers)
    {
        BridgeRecipes.WriteTo(_harness);
        var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, Token);
        var sequence = await Append(sindri, "T-47");
        var address = InstanceAddress.Root(ItemKey.From("T-47"), RecipeId.From("workflow.relay"), 1);
        var rows = new FakeInstances(new InstanceRow(address.Value, "routed", NeedsYou: false, StartedAt: null));
        var held = new HeldItemEvents(rows, sindri.GetRequiredService<TimeProvider>());
        var cursor = sindri.GetRequiredService<EventBridgeCursor>();
        var bridge = new EventBridge(
            sindri.GetRequiredService<ILedgerStore>(), sindri.GetRequiredService<BlockCatalog>(), held, sindri.GetRequiredService<ICommandOrigins>(),
            new RefusingRuntime(answers), cursor, NullLogger<EventBridge>.Instance);
        return (new Bridge(bridge, sindri.GetRequiredService<HeldLease>().Current!), sequence, cursor);
    }

    private async Task<long> Append(IServiceProvider sindri, string item)
    {
        var n = Interlocked.Increment(ref _events);
        var id = Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-7000-8000-{n:D12}"));
        var envelope = new EventEnvelope<string>(id, "item.routed", 1, sindri.GetRequiredService<TimeProvider>().GetUtcNow(), Router, "local", 0, id, CausationId: null, TraceId: null, """{"outcome":"startable"}""");
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var appended = await sindri.GetRequiredService<ILedgerStore>().Append(new LedgerEntry(envelope, "test:" + id.ToString("N"), item), grant, Token);
        Assert.False(appended.IsError, appended.IsError ? appended.Error.Message : null);
        var all = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token);
        return all.Value.Last(e => e.Envelope.Id == id).Sequence;
    }

    /// <summary>A bridge and the lease it runs under.</summary>
    private sealed record Bridge(EventBridge Service, LeaseGrant Grant)
    {
        public ValueTask<Result<ScheduledResult>> Run(CancellationToken ct) => Service.Run(new ScheduledRun(Grant, KnownInstants.Epoch), ct);
    }

    private sealed class FakeInstances(params InstanceRow[] rows) : IWorkflowInstances
    {
        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(rows.ToList());
    }

    /// <summary>Answers every signal with the next of its answers, the last one for good.</summary>
    private sealed class RefusingRuntime(Result<Unit>[] answers) : IInstanceRuntime
    {
        private int _next;

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
            ValueTask.FromResult(answers[Math.Min(_next++, answers.Length - 1)]);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
