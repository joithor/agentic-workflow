using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// What a mirrored workflow event carries beyond the state change (engine spec rev 8.1 §5.6): the trigger's kind, id and
/// actor, any gate, evidence, grant or override data, the effects it runs, and the post-state delta. A public projection of
/// the decision's <see cref="WorkflowEvent"/>: ids, digests, reasons and counters only. It never holds an answer's text, a
/// parked question or a fact's content (the ledger scrubs the payload, and the rows stay free of such text by construction).
/// </summary>
/// <param name="TriggerId">The trigger's key, 'wf:{instance}:{trigger}': carries a command's nonce and arguments digest, a report's run and digest, an event's source sequence.</param>
/// <param name="TriggerActor">Who fired the trigger.</param>
/// <param name="SourceSequence">The ledger sequence of the event that satisfied a wait; none for any other trigger.</param>
/// <param name="ArgumentsSha256">The command's arguments digest, for a command, an answer, a cancel or a handback.</param>
/// <param name="ReportSha256">The report's digest, for a Step completion.</param>
/// <param name="Answer">Which option a 'workflow answer' gave.</param>
/// <param name="Amount">The rounds or tokens of a grant answer.</param>
/// <param name="Reason">A step.failed's reason (already scrubbed by the decision).</param>
/// <param name="Gates">The gates' answers, in order.</param>
/// <param name="Evidence">The evidence checks.</param>
/// <param name="Grant">A grant the trigger made.</param>
/// <param name="Override">An override the trigger made.</param>
/// <param name="Refusal">What followed a refusal.</param>
/// <param name="Effects">The identities of the effects the decision runs for this instance, as '{instance}:{entry}:{index}'.</param>
/// <param name="Delta">The post-state delta.</param>
public sealed record WorkflowEventDetail(
    TriggerKey TriggerId,
    string TriggerActor,
    long? SourceSequence,
    string? ArgumentsSha256,
    string? ReportSha256,
    AnswerVariant? Answer,
    int? Amount,
    string? Reason,
    IReadOnlyList<GateRecord> Gates,
    IReadOnlyList<EvidenceRecord> Evidence,
    GrantRecord? Grant,
    OverrideRecord? Override,
    RefusalRecord? Refusal,
    IReadOnlyList<string> Effects,
    WorkflowEventDelta Delta)
{
    /// <summary>The public projection of a decision's event for <paramref name="trigger"/>.</summary>
    /// <param name="recorded">The decision's event for this instance.</param>
    /// <param name="trigger">The trigger the decision decided; none for a start, whose event carries only its key and actor.</param>
    /// <returns>The detail the ledger row carries.</returns>
    public static WorkflowEventDetail Of(WorkflowEvent recorded, Trigger? trigger)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        var stamp = trigger switch
        {
            CommandFired c => c.Stamp,
            AnswerGiven a => a.Stamp,
            CancelRequested { Stamp: { } s } => s,
            HandbackRequested h => h.Stamp,
            _ => null,
        };
        var answer = trigger as AnswerGiven;
        return new WorkflowEventDetail(
            recorded.Trigger.Key,
            recorded.Trigger.Actor,
            trigger switch
            {
                WaitSatisfied { Source: EventWaitSource e } => e.Sequence,
                CancelRequested { Origin: CancelOrigin.Tracker } c => c.Sequence,
                _ => null,
            },
            stamp?.ArgumentsSha256,
            (trigger as StepCompleted)?.ReportSha256,
            answer?.Variant,
            answer?.Amount,
            recorded.Trigger.Reason,
            recorded.Gates,
            recorded.Evidence,
            recorded.Grant,
            recorded.Override,
            recorded.Refusal,
            [.. recorded.Effects.Select(e => e.Text)],
            WorkflowEventDelta.Of(recorded.Delta));
    }
}
