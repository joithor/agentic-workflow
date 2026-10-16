using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.CloseItem;

/// <summary>An item with an unfinished root instance can't be closed under it: the engine ends it, or the human cancels the instance first (API P11 U4).</summary>
[Lane(TestLane.Unit)]
public sealed class CloseItemActiveInstanceTests
{
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Address => A.FirstItemId.Value + "/step/1";

    private CloseItemService Service() => new(new ViewItemResolver(_h.Views), _h.Slice.Store, _h.Slice.Held, _h.Slice.Events, _h.Slice.Callers);

    [Fact]
    public async Task CloseItem_is_refused_while_an_instance_is_active()
    {
        await Seed();
        await Fold(EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, Aw.Sindri.Core.Workflow.Engine.StepRuntime.Desk), outcome: null));
        using var entered = _h.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(A.CloseItemRequest().Build(), Token);

        Assert.True(result.IsError);
        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Contains($"has an active instance {Address} (fix: sindri workflow cancel {Address})", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task CloseItem_once_the_instance_ended_is_the_ordinary_already_closed_refusal()
    {
        await Seed();
        await Fold(EventTypes.WorkflowStarted, Row("produce", new FrameSummary("produce", 1, "1.1", Child: null, Aw.Sindri.Core.Workflow.Engine.StepRuntime.Desk), outcome: null));
        await Fold(EventTypes.WorkflowTransition, Row("done", new FrameSummary("done", 2, Run: null, Child: null), outcome: "resolved", trigger: "cancel"));
        using var entered = _h.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(A.CloseItemRequest().Build(), Token);

        Assert.True(result.IsError);   // the ended instance already moved the item to its closed state, so the guard has nothing to say
        Assert.Contains("is already cancelled", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("active instance", result.Error.Message, StringComparison.Ordinal);
    }

    private async Task Seed()
    {
        _ = await _h.Slice.HoldLease();
        await _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, "Ship it", Repo: null, Link: null, Body: null, Owner: null));
    }

    private Task Fold(string type, WorkflowEventPayload payload) => _h.Fold(type, payload, A.FirstItemId);

    private static WorkflowEventPayload Row(string to, FrameSummary frame, string? outcome, string trigger = "step.completed")
    {
        var delta = new WorkflowEventDelta(frame, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(Aw.Sindri.Core.Workflow.Engine.TriggerKey.For(Aw.Sindri.Core.Workflow.Engine.WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli",
            SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(Address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
