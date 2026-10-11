using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The frame tests' recipe, instances, suspensions and answers. The recipe: 'produce' (an agent Step in loop 'fix',
/// cap 3, a 4h park timeout, a 200k budget) goes to 'checks' (a desk Step, 1h); 'checks' loops back to 'produce' on
/// 'failed' and ends in 'done'; 'await' waits for 'ci.finished' (2h). The recipe budget is 400k.
/// </summary>
internal static class FrameFixture
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    public static readonly WorkflowInstanceId Id = WorkflowInstanceId.From("WFI-" + new string('0', 30) + "a1");

    public static readonly WorkflowInstanceId ChildId = WorkflowInstanceId.From("WFI-" + new string('0', 30) + "c1");

    public static readonly BlockId Produce = BlockId.From("step.produce");

    public static readonly BlockId Check = BlockId.From("step.check");

    public static readonly BlockId Judge = BlockId.From("gate.verify.judge");

    /// <summary>The index of 'checks -> produce' (the loop transition) in <see cref="Recipe"/>.</summary>
    public const int LoopBack = 1;

    // why one instance: a withheld trigger compares its outputs by reference, so the fixtures share them
    private static IReadOnlyDictionary<FactKey, FactValue> NoOutputs { get; } = new Dictionary<FactKey, FactValue>();

    public static Recipe Recipe { get; } = BuildRecipe();

    public static DecisionContext Context(DateTimeOffset now) => new(
        now, Epoch: 1, ApprovalCheck.Holds,
        new BlockCatalog([TestBlocks.Step("step.produce"), TestBlocks.Step("step.check"), TestBlocks.Gate("gate.verify.judge")], []),
        new HashSet<BlockId> { Produce }, InMemoryChildRecipes.None, EngineTimings.Default);

    /// <summary>An instance at entry 3 in 'produce', run '3.1' on the agent, its state deadline armed at arm 1.</summary>
    public static WorkflowInstance Producing() => Instance(new Frame(
        "produce", 3, new OpenRun(RunToken.For(3, 1), StepRuntime.Agent, "LCH-1", Task: null), Child: null,
        [new Deadline(DeadlineId.For(3, DeadlineKind.State, 1), T0.AddHours(4), Remaining: null)], T0));

    public static WorkflowInstance Instance(Frame frame) => new(
        Id, InstanceAddress.Root(ItemKey.From("T-3"), Recipe.Id, 1), "local", Ring: 0,
        new PinnedRecipe(Recipe.Id, 1, RecipeHash.From("sha256:" + new string('a', 64)), new Dictionary<RecipeId, RecipeHash>()),
        ItemKey.From("T-3"), Attempt: 1, Parent: null, frame, [], new Dictionary<string, LoopState>(StringComparer.Ordinal),
        TokenSpend.None, new Dictionary<FactKey, FactValue>(), Version: 5, Outcome: null);

    public static SuspendOutcome Park(ParkReason reason, ParkKind? kind, ResumeKind resume, WithheldTrigger? withheld = null) => new(
        new Suspension(SuspensionKind.Park, reason, kind, Escalation: null, "checks failed twice", "service:test", Mirrors: null),
        new ResumeAction(resume, withheld, Gate: null, Effect: null));

    public static SuspendOutcome Escalate(EscalationKind kind, ResumeKind resume, WithheldTrigger? withheld = null, BlockId? gate = null) => new(
        new Suspension(SuspensionKind.Escalation, Reason: null, ParkKind: null, kind, "gate.verify.judge scored 0.62", "service:test", Mirrors: null),
        new ResumeAction(resume, withheld, gate, Effect: null));

    public static SuspendOutcome MirrorChild(ParkReason reason) => new(
        new Suspension(SuspensionKind.Park, reason, ParkKind: null, Escalation: null, "child parked", "service:test", ChildId),
        new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effect: null));

    public static WithheldTrigger CheckFailed() => new(LoopBack, new StepCompleted(
        "service:desk", RunToken.For(4, 1), OutcomeName.From("failed"), NoOutputs, Tokens: 10, StepRuntime.Desk,
        new string('b', 64)));

    public static WithheldTrigger CiFinished() => new(Transition: 3, new WaitSatisfied(
        "service:router", new EventWaitSource(EventKind.From("ci.finished"), 42), OutcomeName.From("failed"), NoOutputs));

    public static AnswerGiven Answer(AnswerVariant variant, string? text = null, int? amount = null) => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), variant, text, amount, Forwarded: null);

    /// <summary>The instance suspended by <paramref name="outcome"/> from <paramref name="from"/> (default: <see cref="Producing"/>).</summary>
    public static WorkflowInstance Suspended(SuspendOutcome outcome, WorkflowInstance? from = null) =>
        FrameRules.Suspend(from ?? Producing(), outcome, T0.AddMinutes(30));

    private static Recipe BuildRecipe()
    {
        var states = new Dictionary<string, RecipeState>(StringComparer.Ordinal)
        {
            ["produce"] = new(StateKind.Active, Step(Produce), WaitFor: null, Timeout("4h"), Loop: "fix", new BudgetSpec(200_000), "Produce.", Outcome: null),
            ["checks"] = new(StateKind.Active, Step(Check), WaitFor: null, Timeout("1h"), Loop: null, Budget: null, "Check.", Outcome: null),
            ["await"] = new(StateKind.Wait, Step: null, new WaitFor(EventKind.From("ci.finished"), Answer: null, Resource: null, Child: null),
                Timeout("2h"), Loop: null, Budget: null, "Waiting for CI.", Outcome: null),
            ["done"] = new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From("resolved")),
        };
        return new Recipe(
            1, RecipeId.From("workflow.frames"), 1, Description: null, Base: null, Inputs: [], "produce", states,
            [
                new(["produce"], "checks", "step.completed", ARecipe.When(OutcomeName.From("done")), [], [], [], []),
                new(["checks"], "produce", "step.completed", ARecipe.When(OutcomeName.From("failed")), [], [], [], []),
                new(["checks"], "await", "step.completed", ARecipe.When(OutcomeName.From("done")), [], [], [], []),
                new(["await"], "done", "wait.satisfied", When: null, [], [], [], []),
            ],
            new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["fix"] = new(3, LoopSpec.DefaultNoProgress) },
            new BudgetSpec(400_000), Evals: [], Hash: null);
    }

    private static StepUse Step(BlockId use) => new(use, new Dictionary<string, WithValue>(StringComparer.Ordinal), []);

    private static TimeoutSpec Timeout(string after) => new(RecipeDuration.From(after), TimeoutAction.Park);
}
