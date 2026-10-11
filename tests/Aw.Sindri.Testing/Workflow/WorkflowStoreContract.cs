using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Store;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every <see cref="IWorkflowStore"/> honours (spec §5.3, §10, W12, W13, W21): the in-memory fake (P3 unit (e))
/// and SqliteWorkflowStore (P4) bind it alike, the fake in the unit lane and SQLite in the integration lane. One
/// Commit per trigger in one transaction; a recorded key replays without writing; a moved version is SND-WF-002 and
/// writes nothing, so its key stays unused; a child is created in its parent's Commit; and folding a row's events
/// rebuilds it.
/// </summary>
public abstract class WorkflowStoreContract
{
    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A store over a fresh, empty memory or file.</summary>
    /// <param name="clock">The only clock.</param>
    /// <returns>The store under test.</returns>
    protected abstract ValueTask<IWorkflowStore> Create(TimeProvider clock);

    /// <summary>Replay reproduces the instance (§10, W21): after every Commit, folding the row's events rebuilds the row the store holds.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Folding_the_events_rebuilds_the_row()
    {
        var store = await Create(Clock);
        var row = await Applied(store, StoreRows.Start(StoreRows.Root()));
        await ExpectFoldRebuilds(store, row);
        (string Part, Func<WorkflowInstance, WorkflowInstance> Change)[] steps =
        [
            ("run:1.1:a", Spend),
            ("run:1.1:b", r => StoreRows.Parked(r, ParkReason.Rounds)),
            ("cmd:00000000000000000000000000000001:answer:x", Answer),
            ("run:1.2:c", End),
        ];

        foreach (var (part, change) in steps)
        {
            row = await Applied(store, StoreRows.Move(row, change, StoreRows.Key(row, part)));
            await ExpectFoldRebuilds(store, row);
        }

        Expect.Equal(5, (await store.Events(row.Id, Token)).Count);
    }

    /// <summary>A trigger applied twice is a no-op (§10, W12): the second Commit returns the recorded decision, marked already applied, and writes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Same_trigger_twice_records_one_event()
    {
        var store = await Create(Clock);
        var row = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var key = StoreRows.Key(row, "run:1.1:a");
        var move = StoreRows.Move(row, r => StoreRows.Parked(r, ParkReason.Rounds), key);
        var after = await Applied(store, move);

        var again = (await store.Commit(move, Token)).Value;
        var rebuilt = (await store.Commit(StoreRows.Move(after, r => r with { Outcome = OutcomeName.From("done") }, key), Token)).Value;

        Expect.Equal(new CommitReceipt(move.Decision, AlreadyApplied: true), again);
        Expect.Equal(new CommitReceipt(move.Decision, AlreadyApplied: true), rebuilt);
        Expect.Equal(2, (await store.Events(row.Id, Token)).Count);
        Expect.Equal(2L, (await store.Find(row.Id, Token))!.Version);
        Expect.Equal(move.Decision, await store.Recorded(key, Token));
    }

    /// <summary>A gate refusal is a decision (W12): it is recorded under its key with its code, and its replay returns it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_recorded_refusal_replays_as_refused()
    {
        var store = await Create(Clock);
        var row = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var key = StoreRows.Key(row, "cmd:00000000000000000000000000000002:complete:y");
        var after = StoreRows.Parked(row, ParkReason.NeedsAnswer) with { Version = 2 };
        var refused = new WorkflowCommit(
            new RecordedDecision(key, RecordedKind.Refused, "refused: tests failed", "SND-WF-004"),
            [new InstanceWrite(after, 1, StoreRows.Changed(row, after, key, WorkflowEventType.Refused, new RefusalRecord(RefusalFollow.Park, ParkReason.NeedsAnswer, Escalation: null, Round: null, "SND-WF-004")))],
            [],
            Nonce: null);

        var first = (await store.Commit(refused, Token)).Value;
        var replay = (await store.Commit(refused, Token)).Value;

        Expect.Equal(new CommitReceipt(refused.Decision, AlreadyApplied: false), first);
        Expect.Equal(new CommitReceipt(refused.Decision, AlreadyApplied: true), replay);
        Expect.Equal(WorkflowEventType.Refused, (await store.Events(row.Id, Token))[^1].Type);
    }

    /// <summary>An absorbed trigger is recorded under its key without a row change: no event, the same version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_absorbed_trigger_is_recorded_without_a_row_change()
    {
        var store = await Create(Clock);
        var row = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var key = StoreRows.Key(row, "event:42");
        var absorbed = new WorkflowCommit(new RecordedDecision(key, RecordedKind.Absorbed, "absorbed: not waiting", Code: null), [], [], Nonce: null);

        var receipt = (await store.Commit(absorbed, Token)).Value;

        Expect.Equal(new CommitReceipt(absorbed.Decision, AlreadyApplied: false), receipt);
        Expect.Equal(absorbed.Decision, await store.Recorded(key, Token));
        _ = Expect.Only(await store.Events(row.Id, Token));
        Expect.Equal(1L, (await store.Find(row.Id, Token))!.Version);
    }

    /// <summary>A stale version is refused (§10, §5.3 step 7): SND-WF-002, and the row, its events and the key are as they were.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Commit_refuses_a_stale_version()
    {
        var store = await Create(Clock);
        var started = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var current = await Applied(store, StoreRows.Move(started, r => StoreRows.Parked(r, ParkReason.Rounds), StoreRows.Key(started, "run:1.1:a")));
        var key = StoreRows.Key(started, "run:1.1:b");

        var refused = await store.Commit(StoreRows.Move(started, r => r with { Outcome = OutcomeName.From("done") }, key), Token);

        Expect.Equal(WorkflowErrors.InstanceChanged.Code, refused.Error.Code);
        Expect.True(RowEquality.Same(current, (await store.Find(started.Id, Token))!));
        Expect.Equal(2, (await store.Events(started.Id, Token)).Count);
        Expect.Equal<RecordedDecision?>(expected: null, await store.Recorded(key, Token));
    }

    /// <summary>A bad request never burns a key (W12): a refused Commit records nothing, so the same key applies once the decision is made again from the current row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_refused_commit_leaves_its_key_unused()
    {
        var store = await Create(Clock);
        var started = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var current = await Applied(store, StoreRows.Move(started, r => StoreRows.Parked(r, ParkReason.Rounds), StoreRows.Key(started, "run:1.1:a")));
        var key = StoreRows.Key(started, "cmd:00000000000000000000000000000003:cancel:z");
        static WorkflowInstance Cancel(WorkflowInstance r) => r with { Frame = r.Frame with { State = EngineStates.Cancelled }, Outcome = OutcomeName.From("cancelled") };
        _ = await store.Commit(StoreRows.Move(started, Cancel, key), Token);

        var again = (await store.Commit(StoreRows.Move(current, Cancel, key), Token)).Value;

        Expect.True(!again.AlreadyApplied);
        Expect.Equal(3L, (await store.Find(started.Id, Token))!.Version);
        Expect.Equal(3, (await store.Events(started.Id, Token)).Count);
    }

    /// <summary>A child is created in its parent's Commit (W13): the parent's transition and the child's workflow.started land together.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_child_is_created_in_its_parents_commit()
    {
        var store = await Create(Clock);
        var parent = await Applied(store, StoreRows.Start(StoreRows.Root()));
        var child = StoreRows.Child(parent, 2);

        var receipt = (await store.Commit(StoreRows.Spawn(parent, child, StoreRows.Key(parent, "run:1.1:a")), Token)).Value;

        Expect.True(!receipt.AlreadyApplied);
        var stored = Expect.Only(await store.Children(parent.Id, Token));
        Expect.True(RowEquality.Same(child with { Version = 1 }, stored));
        Expect.True(RowEquality.Same(stored, (await store.FindByAddress(child.Address, Token))!));
        Expect.Equal(WorkflowEventType.Started, Expect.Only(await store.Events(child.Id, Token)).Type);
        Expect.Equal(child.Id, (await store.Find(parent.Id, Token))!.Frame.Child);
        await ExpectFoldRebuilds(store, stored);
        await ExpectFoldRebuilds(store, (await store.Find(parent.Id, Token))!);
    }

    /// <summary>One transaction (W13): when the parent's version moved, the child isn't created either.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_child_is_not_created_when_its_parents_version_moved()
    {
        var store = await Create(Clock);
        var started = await Applied(store, StoreRows.Start(StoreRows.Root()));
        _ = await Applied(store, StoreRows.Move(started, r => StoreRows.Parked(r, ParkReason.Rounds), StoreRows.Key(started, "run:1.1:a")));
        var child = StoreRows.Child(started, 2);

        var refused = await store.Commit(StoreRows.Spawn(started, child, StoreRows.Key(started, "run:1.1:b")), Token);

        Expect.Equal(WorkflowErrors.InstanceChanged.Code, refused.Error.Code);
        Expect.Equal<WorkflowInstance?>(expected: null, await store.Find(child.Id, Token));
        Expect.Equal(0, (await store.Children(started.Id, Token)).Count);
        Expect.Equal(0, (await store.Events(child.Id, Token)).Count);
    }

    /// <summary>Creating a row that exists (from version 0) is a moved version too, and writes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Starting_an_existing_instance_is_refused()
    {
        var store = await Create(Clock);
        var root = StoreRows.Root();
        _ = await Applied(store, StoreRows.Start(root));

        var refused = await store.Commit(StoreRows.Start(root, StoreRows.Key(root, "start:2")), Token);

        Expect.Equal(WorkflowErrors.InstanceChanged.Code, refused.Error.Code);
        _ = Expect.Only(await store.Events(root.Id, Token));
    }

    /// <summary>An instance the store never wrote reads as absent: no row, no children, no events, no recorded key.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_instance_reads_as_absent()
    {
        var store = await Create(Clock);
        var root = StoreRows.Root();

        Expect.Equal<WorkflowInstance?>(expected: null, await store.Find(root.Id, Token));
        Expect.Equal<WorkflowInstance?>(expected: null, await store.FindByAddress(root.Address, Token));
        Expect.Equal(0, (await store.Children(root.Id, Token)).Count);
        Expect.Equal(0, (await store.Events(root.Id, Token)).Count);
        Expect.Equal<RecordedDecision?>(expected: null, await store.Recorded(StoreRows.Key(root, "start:1"), Token));
    }

    private static WorkflowInstance Spend(WorkflowInstance row) =>
        row with { Spent = new TokenSpend(1200, new Dictionary<string, int>(StringComparer.Ordinal) { ["produce"] = 1200 }, 0) };

    private static WorkflowInstance Answer(WorkflowInstance row) => row with
    {
        Frame = row.Suspended[0].Frame with { Deadlines = [new Deadline(DeadlineId.For(1, DeadlineKind.State, 2), StoreRows.Now.AddHours(5), Remaining: null)] },
        Suspended = [],
        Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["rounds"] = new LoopState(2, 4, new ProgressMark("f1", Failure: null, EmptyDiff: false)) },
        Facts = new Dictionary<FactKey, FactValue>(row.Facts) { [FactKey.Answer] = StoreRows.Fact('2'), [StoreRows.Findings] = StoreRows.Fact('3') },
    };

    private static WorkflowInstance End(WorkflowInstance row) => row with
    {
        Frame = new Frame("done", 2, Run: null, Child: null, Deadlines: [], StoreRows.Now.AddHours(6)),
        Facts = new Dictionary<FactKey, FactValue>(row.Facts) { [StoreRows.Findings] = StoreRows.Fact('4') },
        Outcome = OutcomeName.From("done"),
    };

    private static async Task<WorkflowInstance> Applied(IWorkflowStore store, WorkflowCommit commit)
    {
        var receipt = (await store.Commit(commit, Token)).Value;
        Expect.True(!receipt.AlreadyApplied);
        return (await store.Find(commit.Writes[0].After.Id, Token))!;
    }

    private static async Task ExpectFoldRebuilds(IWorkflowStore store, WorkflowInstance row)
    {
        var folded = EventFold.Fold(await store.Events(row.Id, Token));
        Expect.True(RowEquality.Same(row, folded));
    }
}
