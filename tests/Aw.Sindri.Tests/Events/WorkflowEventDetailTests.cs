using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Events;

/// <summary>
/// The public projection of a decision's event that a mirrored ledger row carries (engine spec rev 8.1 §5.6; review run 2,
/// architecture-strategist-r2-1, data-integrity-guardian-r2-2): ids and digests, never text.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowEventDetailTests
{
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.From("WFI-" + new string('a', 32));

    [Fact]
    public void A_refused_row_names_its_gate_and_what_followed()
    {
        var gate = new GateRecord(BlockId.From("gate.route.startable"), Verdict: null, Score: null, Threshold: null, [], GateOutcome.Unavailable);
        var refusal = new RefusalRecord(RefusalFollow.Park, ParkReason.Rounds, Escalation: null, Round: 3, Code: null);
        var recorded = Event(WorkflowEventType.Refused, "step.completed", gates: [gate], refusal: refusal);

        var detail = WorkflowEventDetail.Of(recorded, Report());

        Assert.Equal("gate.route.startable", Assert.Single(detail.Gates).Gate.Value);
        Assert.Equal(GateOutcome.Unavailable, detail.Gates[0].Outcome);
        Assert.Equal((RefusalFollow.Park, ParkReason.Rounds, 3), (detail.Refusal!.Follow, detail.Refusal.Park, detail.Refusal.Round));
        Assert.Equal(new string('f', 64), detail.ReportSha256);
    }

    [Fact]
    public void A_park_row_names_its_reason_and_never_the_question()
    {
        var parked = new Suspension(SuspensionKind.Park, ParkReason.Budget, ParkKind.Loop, Escalation: null, "the secret question text", "service:engine", Mirrors: null, new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));
        var recorded = Event(WorkflowEventType.Transition, "step.completed", delta: new InstanceDelta(Frame: null, parked, PendingPark: null, Loops: null, Spent: null, Facts: null, Outcome: null));

        var detail = WorkflowEventDetail.Of(recorded, Report());

        Assert.Equal((SuspensionKind.Park, ParkReason.Budget, ParkKind.Loop), (detail.Delta.Suspension!.Kind, detail.Delta.Suspension.Reason, detail.Delta.Suspension.ParkKind));
        Assert.DoesNotContain("secret question", System.Text.Json.JsonSerializer.Serialize(detail, SliceHarness.Json), StringComparison.Ordinal);
    }

    [Fact]
    public void A_wait_satisfied_row_names_its_source_sequence()
    {
        var satisfied = new WaitSatisfied("service:bridge", new EventWaitSource(EventKind.From("item.routed"), 4711), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());

        var detail = WorkflowEventDetail.Of(Event(WorkflowEventType.Transition, "wait.satisfied"), satisfied);

        Assert.Equal(4711, detail.SourceSequence);
    }

    [Fact]
    public void A_grant_row_names_the_variant_and_the_amount_and_a_command_its_arguments_digest()
    {
        var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64));
        var answer = new AnswerGiven("human:cli", stamp, AnswerVariant.Grant, Text: "free text that stays out", Amount: 2, Forwarded: null);

        var detail = WorkflowEventDetail.Of(Event(WorkflowEventType.Transition, "answer", grant: new GrantRecord(Rounds: 2, Tokens: null)), answer);

        Assert.Equal((AnswerVariant.Grant, 2, 2, new string('d', 64)), (detail.Answer, detail.Amount, detail.Grant!.Rounds, detail.ArgumentsSha256));
        Assert.DoesNotContain("free text", System.Text.Json.JsonSerializer.Serialize(detail, SliceHarness.Json), StringComparison.Ordinal);
    }

    [Fact]
    public void A_forwarded_answer_row_names_the_parent_it_came_from()
    {
        var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64));
        var forwarded = new AnswerGiven("human:cli", stamp, AnswerVariant.Grant, Text: null, Amount: 1, new OnBehalfOf(_instance, "human:cli"));
        var direct = forwarded with { Forwarded = null };

        var viaParent = WorkflowEventDetail.Of(Event(WorkflowEventType.Transition, "answer"), forwarded);
        var own = WorkflowEventDetail.Of(Event(WorkflowEventType.Transition, "answer"), direct);

        Assert.Equal((_instance.Value, null), (viaParent.OnBehalfOf, own.OnBehalfOf));
        Assert.DoesNotContain("onBehalfOf", System.Text.Json.JsonSerializer.Serialize(own, SliceHarness.Json), StringComparison.Ordinal);
    }

    [Fact]
    public void The_facts_a_row_added_are_their_digests_only()
    {
        var facts = new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = new("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null) };
        var detail = WorkflowEventDetail.Of(Event(WorkflowEventType.Transition, "step.completed", delta: new InstanceDelta(Frame: null, Suspension: null, PendingPark: null, Loops: null, Spent: null, facts, Outcome: null)), Report());

        Assert.Equal(new string('e', 64), detail.Delta.Facts!["fact:output"]);
    }

    [Fact]
    public void An_agent_report_records_via_agent_and_tokens()
    {
        var report = new StepCompleted("session:claude", RunToken.For(3, 1), OutcomeName.From("done"), Output(), Tokens: 1_200, StepRuntime.Agent, new string('a', 64));

        var recorded = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(FrameFixture.Producing(), FrameFixture.Recipe, report, GateAnswers.None, FrameFixture.Context()))).Changes[0].Event;
        var detail = WorkflowEventDetail.Of(recorded, report);

        Assert.Equal(("agent", 1_200), (recorded.Trigger.Via, recorded.Trigger.Tokens));
        Assert.Equal(("agent", 1_200), (detail.Via, detail.Tokens));
        var json = System.Text.Json.JsonSerializer.Serialize(detail, SliceHarness.Json);
        Assert.Contains("\"via\":\"agent\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tokens\":1200", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_desk_report_writes_no_via()
    {
        var instance = FrameFixture.Instance(new Frame("checks", Entry: 4, new OpenRun(RunToken.For(4, 1), StepRuntime.Desk, Launch: null, Task: "desk-1"), Child: null));
        var report = new StepCompleted("human:cli", RunToken.For(4, 1), OutcomeName.From("done"), Output(), Tokens: 0, StepRuntime.Desk, new string('b', 64));

        var recorded = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(instance, FrameFixture.Recipe, report, GateAnswers.None, FrameFixture.Context()))).Changes[0].Event;
        var detail = WorkflowEventDetail.Of(recorded, report);

        Assert.Equal((null, null), (detail.Via, detail.Tokens));
        var json = System.Text.Json.JsonSerializer.Serialize(detail, SliceHarness.Json);
        Assert.DoesNotContain("\"via\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tokens\":", json, StringComparison.Ordinal);
    }

    private static Dictionary<FactKey, FactValue> Output() => new() { [FactKey.From("fact:output")] = EngineFixture.Fact("the output") };

    private static StepCompleted Report() =>
        new("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('f', 64));

    private static WorkflowEvent Event(WorkflowEventType type, string kind, IReadOnlyList<GateRecord>? gates = null, RefusalRecord? refusal = null, GrantRecord? grant = null, InstanceDelta? delta = null) =>
        new(type, _instance, new TriggerRecord(kind, TriggerKey.For(_instance, "run:1.1:" + new string('f', 64)), "human:cli", OutcomeName.From("done")), Started: null,
            delta ?? new InstanceDelta(Frame: null, Suspension: null, PendingPark: null, Loops: null, Spent: null, Facts: null, Outcome: null), gates ?? [], [], grant, Override: null, refusal, []);
}
