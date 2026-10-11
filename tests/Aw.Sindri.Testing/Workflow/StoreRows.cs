using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Store;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// Instance rows, their events and the Commits that write them, for the store contract and the fold tests (spec §5.1–§5.3).
/// Rows hold no withheld trigger, so a store that round-trips them through JSON still compares them structurally.
/// </summary>
public static class StoreRows
{
    /// <summary>The recipe every root runs.</summary>
    public static readonly RecipeId ItemRecipe = RecipeId.From("workflow.item");

    /// <summary>The recipe every child runs.</summary>
    public static readonly RecipeId StepRecipe = RecipeId.From("workflow.step");

    /// <summary>The instant rows start at.</summary>
    public static readonly DateTimeOffset Now = KnownInstants.Epoch;

    /// <summary>A fact the root starts with.</summary>
    public static readonly FactKey Brief = FactKey.From("fact:brief");

    /// <summary>A fact a later round adds.</summary>
    public static readonly FactKey Findings = FactKey.From("fact:findings");

    /// <summary>The epoch every event is written under.</summary>
    public const long Epoch = 1;

    /// <summary>The instance id numbered <paramref name="n"/>.</summary>
    /// <param name="n">A number from 1.</param>
    /// <returns>'WFI-' and <paramref name="n"/> in 32 hex digits.</returns>
    public static WorkflowInstanceId Id(int n) => WorkflowInstanceId.From("WFI-" + n.ToString("x32", CultureInfo.InvariantCulture));

    /// <summary>A fact value whose digest is <paramref name="digit"/> repeated.</summary>
    /// <param name="digit">A hex digit.</param>
    /// <returns>The value.</returns>
    public static FactValue Fact(char digit) => new("blob:" + digit, new string(digit, 64), new string(digit, 40));

    /// <summary>
    /// A root at version 0, in 'produce' entry 1 with an open desk run and a state deadline, round 1 of 'rounds', and
    /// the brief; <see cref="Start"/> writes it at version 1.
    /// </summary>
    /// <param name="n">The instance number; also its attempt, so each root has an address of its own.</param>
    /// <returns>The row.</returns>
    public static WorkflowInstance Root(int n = 1) => new(
        Id(n),
        InstanceAddress.Root(ItemKey.From("T-3"), ItemRecipe, n),
        "default",
        Ring: 0,
        new PinnedRecipe(ItemRecipe, 1, Hash('a'), new Dictionary<RecipeId, RecipeHash> { [StepRecipe] = Hash('b') }),
        ItemKey.From("T-3"),
        Attempt: n,
        Parent: null,
        Running("produce", 1),
        Suspended: [],
        new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["rounds"] = new LoopState(1, 3, Last: null) },
        TokenSpend.None,
        new Dictionary<FactKey, FactValue> { [Brief] = Fact('1') },
        Version: 0,
        Outcome: null);

    /// <summary>A child of <paramref name="parent"/> at version 0, created by the parent's current entry.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="n">The child's instance number.</param>
    /// <returns>The row.</returns>
    public static WorkflowInstance Child(WorkflowInstance parent, int n) => Root(n) with
    {
        Address = parent.Address.Child(StepRecipe, parent.Frame.Entry),
        Recipe = new PinnedRecipe(StepRecipe, 1, Hash('b'), new Dictionary<RecipeId, RecipeHash>()),
        Attempt = parent.Attempt,
        Parent = parent.Id,
    };

    /// <summary>A recipe frame in <paramref name="state"/> with an open desk run and a state deadline.</summary>
    /// <param name="state">The state.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The frame.</returns>
    public static Frame Running(string state, int entry) => new(
        state,
        entry,
        new OpenRun(RunToken.For(entry, 1), StepRuntime.Desk, Launch: null, Task: state + " T-3"),
        Child: null,
        [new Deadline(DeadlineId.For(entry, DeadlineKind.State, 1), Now.AddHours(entry), Remaining: null)],
        Now.AddMinutes(entry));

    /// <summary>The row parked for <paramref name="reason"/>: its frame pushed with its deadlines cleared.</summary>
    /// <param name="row">The row.</param>
    /// <param name="reason">The park reason.</param>
    /// <returns>The parked row (same version).</returns>
    public static WorkflowInstance Parked(WorkflowInstance row, ParkReason reason) => row with
    {
        Frame = new Frame(EngineStates.Parked, row.Frame.Entry, Run: null, Child: null, Deadlines: [], Now.AddHours(2)),
        Suspended =
        [
            new SuspendedFrame(
                row.Frame with { Deadlines = [] },
                new Suspension(SuspensionKind.Park, reason, ParkKind.Loop, Escalation: null, "rounds used up", "service:engine", Mirrors: null),
                new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effect: null),
                Pending: null),
        ],
    };

    /// <summary>The key of <paramref name="part"/> on <paramref name="row"/>.</summary>
    /// <param name="row">The row.</param>
    /// <param name="part">The trigger's own id.</param>
    /// <returns>'wf:{instance}:{part}'.</returns>
    public static TriggerKey Key(WorkflowInstance row, string part) => TriggerKey.For(row.Id, part);

    /// <summary>The workflow.started event of <paramref name="row"/> (already at its first version).</summary>
    /// <param name="row">The new row.</param>
    /// <param name="key">The trigger's key.</param>
    /// <returns>The event.</returns>
    public static WorkflowEvent Started(WorkflowInstance row, TriggerKey key) => new(
        WorkflowEventType.Started, row.Id, Trigger("start", key), FromVersion: 0, row.Version, Epoch, row, EventFold.Delta(before: null, row),
        [], [], Grant: null, Override: null, Refusal: null, Outbox: []);

    /// <summary>The event that takes <paramref name="before"/> to <paramref name="after"/> (already at its raised version).</summary>
    /// <param name="before">The row before.</param>
    /// <param name="after">The row after.</param>
    /// <param name="key">The trigger's key.</param>
    /// <param name="type">The event type.</param>
    /// <param name="refusal">What followed a refusal, for workflow.refused.</param>
    /// <returns>The event.</returns>
    public static WorkflowEvent Changed(WorkflowInstance before, WorkflowInstance after, TriggerKey key, WorkflowEventType type = WorkflowEventType.Transition, RefusalRecord? refusal = null) => new(
        type, after.Id, Trigger("step.completed", key), before.Version, after.Version, Epoch, Started: null, EventFold.Delta(before, after),
        [], [], Grant: null, Override: null, refusal, Outbox: []);

    /// <summary>The Commit that starts <paramref name="row"/> at version 1.</summary>
    /// <param name="row">The row at version 0.</param>
    /// <param name="key">The trigger's key; 'start:1' when omitted.</param>
    /// <returns>The Commit.</returns>
    public static WorkflowCommit Start(WorkflowInstance row, TriggerKey? key = null)
    {
        var at = key ?? Key(row, "start:1");
        var started = row with { Version = 1 };
        return new WorkflowCommit(new RecordedDecision(at, RecordedKind.Applied, "started " + started.State, Code: null), [new InstanceWrite(started, 0, Started(started, at))], [], Nonce: null);
    }

    /// <summary>The Commit that changes <paramref name="before"/> by <paramref name="change"/> under <paramref name="key"/>, raising its version.</summary>
    /// <param name="before">The row as the decision read it.</param>
    /// <param name="change">The change.</param>
    /// <param name="key">The trigger's key.</param>
    /// <returns>The Commit.</returns>
    public static WorkflowCommit Move(WorkflowInstance before, Func<WorkflowInstance, WorkflowInstance> change, TriggerKey key)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(change);
        var after = change(before) with { Version = before.Version + 1 };
        return new WorkflowCommit(
            new RecordedDecision(key, RecordedKind.Applied, "-> " + after.State, Code: null),
            [new InstanceWrite(after, before.Version, Changed(before, after, key))],
            [],
            Nonce: null);
    }

    /// <summary>The Commit that moves <paramref name="parent"/> into a child wait and creates <paramref name="child"/> in the same transaction (W13).</summary>
    /// <param name="parent">The parent as the decision read it.</param>
    /// <param name="child">The new child at version 0.</param>
    /// <param name="key">The parent's trigger key.</param>
    /// <returns>The Commit.</returns>
    public static WorkflowCommit Spawn(WorkflowInstance parent, WorkflowInstance child, TriggerKey key)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        var waiting = parent with
        {
            Frame = new Frame("steps", parent.Frame.Entry + 1, Run: null, child.Id, Deadlines: [], Now.AddHours(3)),
            Version = parent.Version + 1,
        };
        var started = child with { Version = 1 };
        return new WorkflowCommit(
            new RecordedDecision(key, RecordedKind.Applied, "-> steps", Code: null),
            [new InstanceWrite(waiting, parent.Version, Changed(parent, waiting, key)), new InstanceWrite(started, 0, Started(started, key))],
            [],
            Nonce: null);
    }

    private static TriggerRecord Trigger(string kind, TriggerKey key) => new(kind, key, "human:thor", Outcome: null);

    private static RecipeHash Hash(char digit) => RecipeHash.From("sha256:" + new string(digit, 64));
}
