using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>
/// The payload of a workflow ledger event (spec §5.2): one per instance a Commit changes. Every event carries the
/// trigger, the version step and the epoch, any gate, evidence, grant or override data, the outbox ids, and the
/// post-state delta (W21). The ledger writer scrubs the whole payload (E20).
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="Instance">The instance it changed.</param>
/// <param name="Trigger">The trigger.</param>
/// <param name="FromVersion">The row's version before (0 for workflow.started).</param>
/// <param name="ToVersion">The row's version after.</param>
/// <param name="Epoch">The lease epoch of the writer.</param>
/// <param name="Started">The whole new row, for workflow.started; the base the deltas fold over.</param>
/// <param name="Delta">The post-state delta.</param>
/// <param name="Gates">The gates' answers, in order.</param>
/// <param name="Evidence">The evidence checks.</param>
/// <param name="Grant">A grant the trigger made.</param>
/// <param name="Override">An override the trigger made.</param>
/// <param name="Refusal">What followed a refusal, for workflow.refused.</param>
/// <param name="Outbox">The outbox commands the Commit wrote for this instance.</param>
public sealed record WorkflowEvent(
    WorkflowEventType Type,
    WorkflowInstanceId Instance,
    TriggerRecord Trigger,
    long FromVersion,
    long ToVersion,
    long Epoch,
    WorkflowInstance? Started,
    InstanceDelta Delta,
    IReadOnlyList<GateRecord> Gates,
    IReadOnlyList<EvidenceRecord> Evidence,
    GrantRecord? Grant,
    OverrideRecord? Override,
    RefusalRecord? Refusal,
    IReadOnlyList<OutboxCommandId> Outbox);
