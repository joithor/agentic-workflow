using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The item states a root instance's ledger rows give an item (API spec section 3.6, API P11): an open desk run needs an answer,
/// an agent run or a wait is running, a park or escalation is parked, a takeover is taken over, and an ended instance is done
/// (cancelled when a cancel ended it). Pure folds of the rows: a delta that leaves a part out keeps the part that came before.
/// Rows from before 'FrameSummary.Runtime' with an open run read as desk, the only runtime before the agent runtime (P8 must re-check).
/// </summary>
public static class EngineItemStates
{
    /// <summary>The part after an event, or null when the event leaves the item alone: a child instance's row, a row of an earlier attempt, or a row after the instance ended.</summary>
    /// <param name="prior">The part so far, or null before the first row.</param>
    /// <param name="eventType">The ledger event type.</param>
    /// <param name="payload">The event's payload.</param>
    /// <returns>The new part, or null.</returns>
    public static EngineItemPart? Next(EngineItemPart? prior, string eventType, WorkflowEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(payload);
        var started = string.Equals(eventType, EventTypes.WorkflowStarted, StringComparison.Ordinal);
        if (!IsRoot(payload.Instance) || !(started || Continues(prior, payload.Instance)))
        {
            return null;
        }

        return Build(payload, started ? null : prior);
    }

    private static EngineItemPart Build(WorkflowEventPayload payload, EngineItemPart? carried)
    {
        var frame = payload.Detail?.Delta.Frame;
        var (runOpen, runtime) = Run(frame, carried);
        var ended = payload.Detail?.Delta.Outcome is not null;
        return new EngineItemPart(
            payload.Instance,
            frame?.State ?? carried?.State ?? payload.To,
            runOpen,
            runtime,
            Suspension(payload.Detail, carried),
            ended,
            ended && Cancels(payload.Trigger));
    }

    private static bool Cancels(string trigger) => string.Equals(trigger, TriggerNames.Cancel, StringComparison.Ordinal);

    private static SuspensionKind? Suspension(WorkflowEventDetail? detail, EngineItemPart? carried) =>
        detail is null ? carried?.Suspension : detail.Delta.Suspension?.Kind;

    private static bool Continues(EngineItemPart? prior, string address) =>
        prior is { Ended: false } && string.Equals(prior.Address, address, StringComparison.Ordinal);

    private static (bool Open, StepRuntime? Runtime) Run(FrameSummary? frame, EngineItemPart? carried) => frame switch
    {
        { Run: null } => (false, null),
        { } open => (true, open.Runtime ?? StepRuntime.Desk),
        null => (carried?.RunOpen ?? false, carried?.Runtime),
    };

    /// <summary>The item state a part reads as.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The state.</returns>
    public static ItemState StateOf(EngineItemPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return part switch
        {
            { Ended: true, Cancelled: true } => ItemState.Cancelled,
            { Ended: true } => ItemState.Done,
            { Suspension: SuspensionKind.Takeover } => ItemState.TakenOver,
            { Suspension: SuspensionKind.Park or SuspensionKind.Escalation } => ItemState.Parked,
            { RunOpen: true, Runtime: StepRuntime.Desk } => ItemState.NeedsAnswer,
            _ => ItemState.Running,
        };
    }

    /// <summary>Whether <paramref name="address"/> is a root instance's: '{item}/{recipe}/{attempt}', with no child generation after it.</summary>
    /// <param name="address">The address.</param>
    /// <returns>True for a root.</returns>
    internal static bool IsRoot(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.AsSpan().Count('/') == 2;
    }
}
