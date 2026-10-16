using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Tests.Items;

/// <summary>The item state a root instance's ledger rows give an item (API spec section 3.6; API P11 U2).</summary>
[Trait("Lane", "unit")]
public sealed class EngineItemStatesTests
{
    private const string Address = "ITM-11111111111111111111111111111111/step/1";

    [Fact]
    public void Start_into_a_wait_state_reads_running()
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("triaged", frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        Assert.Equal(ItemState.Running, EngineItemStates.StateOf(part!));
        Assert.Equal((Address, "triaged"), (part!.Address, part.State));
    }

    [Theory]
    [InlineData(StepRuntime.Desk, ItemState.NeedsAnswer)]
    [InlineData(StepRuntime.Agent, ItemState.Running)]
    public void An_open_run_reads_by_who_runs_it(StepRuntime runtime, ItemState expected)
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, runtime)));

        Assert.Equal(expected, EngineItemStates.StateOf(part!));
    }

    [Fact]
    public void Open_desk_run_reads_needs_answer_and_a_row_from_before_the_runtime_field_reads_as_desk()
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null)));

        Assert.Equal(ItemState.NeedsAnswer, EngineItemStates.StateOf(part!));
    }

    [Theory]
    [InlineData(SuspensionKind.Park)]
    [InlineData(SuspensionKind.Escalation)]
    public void Park_and_escalation_read_parked(SuspensionKind kind)
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var parked = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(kind, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));

        Assert.Equal(ItemState.Parked, EngineItemStates.StateOf(parked!));
    }

    [Fact]
    public void Takeover_reads_taken_over_and_handback_returns_to_the_frame_state()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var taken = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Mirrors: null)));
        var handed = EngineItemStates.Next(taken, EventTypes.WorkflowTransition, Row("produce"));

        Assert.Equal(ItemState.TakenOver, EngineItemStates.StateOf(taken!));
        Assert.Equal(ItemState.NeedsAnswer, EngineItemStates.StateOf(handed!));
    }

    [Theory]
    [InlineData(TriggerNames.Cancel, ItemState.Cancelled)]
    [InlineData("step.completed", ItemState.Done)]
    public void Cancel_ends_cancelled_and_any_other_terminal_ends_done(string trigger, ItemState expected)
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var ended = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("done", trigger: trigger, frame: new FrameSummary("done", 2, Run: null, Child: null), outcome: "resolved"));

        Assert.Equal(expected, EngineItemStates.StateOf(ended!));
    }

    [Fact]
    public void Child_rows_leave_the_item_alone()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("triaged", frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        var child = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", address: Address + "/build.1", suspension: new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));

        Assert.Null(child);
    }

    private static WorkflowEventPayload Row(string to, string? address = null, string trigger = "step.completed", FrameSummary? frame = null, SuspensionSummary? suspension = null, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address ?? Address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
