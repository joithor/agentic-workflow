using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A 'workflow.item' parent waiting in 'running' on its 'workflow.step' child (spec §12), and the frames a child's
/// changes put on it, built by hand so the child rules are tested apart from the frame rules.
/// </summary>
internal static class ChildFixture
{
    public const string Inbox = "service:workflow.inbox";

    public const string Terminal = "human:cli";

    public static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Later = Now.AddHours(3);

    public static readonly WorkflowInstanceId ParentId = WorkflowInstanceId.From("WFI-" + new string('a', 32));

    public static readonly WorkflowInstanceId ChildId = WorkflowInstanceId.From("WFI-" + new string('c', 32));

    public static readonly WorkflowInstanceId OtherChildId = WorkflowInstanceId.From("WFI-" + new string('d', 32));

    public static readonly RecipeId Step = RecipeId.From("workflow.step");

    public static readonly RecipeHash StepPin = RecipeHash.From("sha256:" + new string('5', 64));

    public static readonly FactKey StepSpec = FactKey.From("fact:stepSpec");

    public static readonly FactKey Spec = FactKey.From("fact:spec");

    public static readonly TriggerKey EnteringKey = TriggerKey.For(ParentId, "event:42");

    /// <summary>The 'running' wait's entry.</summary>
    public const int Entry = 4;

    private static readonly Lazy<Recipe> _item = new(() =>
        RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.item.yaml"), "workflow.item.yaml").Recipe);

    private static readonly Lazy<Recipe> _step = new(() =>
        RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Recipe);

    /// <summary>The shipped 'workflow.item' ('running' waits on a workflow.step child, onParked: propagate).</summary>
    public static Recipe Item => _item.Value;

    /// <summary>The shipped 'workflow.step'.</summary>
    public static Recipe StepRecipe => _step.Value;

    /// <summary>'workflow.item' with the child wait's onParked set to <paramref name="onParked"/>.</summary>
    public static Recipe ItemWith(OnParked onParked)
    {
        var running = Item.States["running"];
        var wait = running.WaitFor!;
        var states = new Dictionary<string, RecipeState>(Item.States, StringComparer.Ordinal)
        {
            ["running"] = running with { WaitFor = wait with { Child = wait.Child! with { OnParked = onParked } } },
        };
        return Item with { States = states };
    }

    /// <summary>The child wait of 'running'.</summary>
    public static ChildWait Wait => Item.States["running"].WaitFor!.Child!;

    /// <summary>The fact the parent recorded for 'fact:stepSpec'.</summary>
    public static FactValue StepSpecValue { get; } = new("blob:spec", new string('e', 64), HeadSha: null);

    /// <summary>The 'running' frame as entering it left it, waiting on <see cref="ChildId"/>.</summary>
    public static Frame Running { get; } = new(
        "running", Entry, Run: null, ChildId, [new Deadline(DeadlineId.For(Entry, DeadlineKind.State, 1), Now.AddDays(14), Remaining: null)], Now);

    /// <summary>The parent waiting in 'running' on <see cref="ChildId"/>.</summary>
    public static WorkflowInstance Waiting { get; } = new(
        ParentId,
        InstanceAddress.From("T-3/item/1"),
        "default",
        Ring: 0,
        new PinnedRecipe(RecipeId.From("workflow.item"), 1, RecipeHash.From("sha256:" + new string('1', 64)), new Dictionary<RecipeId, RecipeHash> { [Step] = StepPin }),
        ItemKey.From("T-3"),
        Attempt: 1,
        Parent: null,
        Running,
        Suspended: [],
        new Dictionary<string, LoopState>(StringComparer.Ordinal),
        TokenSpend.None,
        new Dictionary<FactKey, FactValue> { [StepSpec] = StepSpecValue },
        Version: 6,
        Outcome: null);

    /// <summary>A child change delivered through the inbox.</summary>
    public static ChildChanged Changed(ChildChange change, ParkReason? reason = null, string? question = null, WorkflowInstanceId? child = null) =>
        new(Inbox, child ?? ChildId, change, reason, question, TriggerKey.For(child ?? ChildId, "run:1.2:" + new string('0', 64)));

    /// <summary>Pushes <paramref name="outcome"/> over the current frame, as the frame rules do: the frame below keeps its id-only, stopped deadlines.</summary>
    public static WorkflowInstance Push(WorkflowInstance instance, SuspendOutcome outcome)
    {
        var below = instance.Frame with { Deadlines = [.. instance.Frame.Deadlines.Select(d => new Deadline(d.Id, Due: null, Remaining: null))] };
        var state = outcome.Suspension.Kind switch
        {
            SuspensionKind.Escalation => EngineStates.Escalated,
            SuspensionKind.Takeover => EngineStates.TakenOver,
            SuspensionKind.Park or _ => EngineStates.Parked,
        };
        return instance with
        {
            Suspended = [.. instance.Suspended, new SuspendedFrame(below, outcome.Suspension, outcome.Resume, Pending: null)],
            Frame = new Frame(state, below.Entry, Run: null, outcome.Suspension.Mirrors, [], Now),
            Version = instance.Version + 1,
        };
    }

    /// <summary>The parent mirroring the child's no-progress park.</summary>
    public static WorkflowInstance MirroringPark { get; } = Push(Waiting, new SuspendOutcome(
        new Suspension(SuspensionKind.Park, ParkReason.NoProgress, ParkKind: null, Escalation: null, "checks failed twice with the same findings", Inbox, ChildId),
        new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effect: null)));

    /// <summary>The parent mirroring the child's escalation.</summary>
    public static WorkflowInstance MirroringEscalation { get; } = Push(Waiting, new SuspendOutcome(
        new Suspension(SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Child, "gate.verify.judge scored 0.62", Inbox, ChildId),
        new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effect: null)));

    /// <summary><paramref name="instance"/> taken over by <see cref="Terminal"/>.</summary>
    public static WorkflowInstance TakenOver(WorkflowInstance instance) => Push(instance, new SuspendOutcome(
        new Suspension(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, string.Empty, Terminal, Mirrors: null),
        new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effect: null)));

    /// <summary>A command stamp with a nonce made of <paramref name="digit"/>.</summary>
    public static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('f', 64));

    /// <summary>A child of <see cref="Waiting"/> in <paramref name="state"/>.</summary>
    public static WorkflowInstance Child(WorkflowInstanceId id, string state, WorkflowInstanceId? parent = null, OutcomeName? outcome = null) => Waiting with
    {
        Id = id,
        Address = InstanceAddress.From("T-3/item/1/step." + Entry.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Parent = parent ?? ParentId,
        Frame = new Frame(state, 2, Run: null, Child: null, [], Now),
        Outcome = outcome,
    };

    /// <summary>A literal child input.</summary>
    public static WithValue Literal(string json) => new(JsonDocument.Parse(json).RootElement.Clone(), Fact: null, Profile: null);
}
