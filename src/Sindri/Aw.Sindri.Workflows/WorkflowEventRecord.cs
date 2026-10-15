using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// One event the interpreter mirrors into the ledger through <see cref="RecordEventActivities"/> (engine spec rev 8.1
/// §5.6). It carries no key: the activity keys it by its own ActivityInfo (<see cref="RecordEventActivities.KeyOf"/>).
/// </summary>
/// <param name="WorkflowId">The workflow id: the instance address.</param>
/// <param name="Type">The event type: 'workflow.started', 'workflow.transition', 'workflow.refused' (spec §5.6).</param>
/// <param name="Item">The work item.</param>
/// <param name="From">The state left ('' for a start).</param>
/// <param name="To">The state entered.</param>
/// <param name="Trigger">The trigger's kind ('start', 'step.completed', 'wait.satisfied', 'answer').</param>
/// <param name="Outcome">The trigger's outcome, when it has one.</param>
/// <param name="Run">The run token the trigger reported on, when it has one.</param>
/// <param name="Actor">Who fired the trigger (the human who answered or reported, the timer service); none records the process's own actor.</param>
/// <param name="Tenant">The instance's tenant; none records the process's.</param>
/// <param name="Ring">The instance's ring; none records the process's.</param>
/// <param name="Detail">The rest of the event the decision recorded (spec §5.6); none for a row mirrored without a decision (a held event's expiry, the desk deadline's park).</param>
/// <param name="Causation">What caused it beyond the trigger: the launch ('LCH-…') an open-request or a start taken over answers (UI E-6, E-19).</param>
internal sealed record WorkflowEventRecord(
    string WorkflowId,
    string Type,
    string Item,
    string From,
    string To,
    string Trigger,
    string? Outcome,
    string? Run,
    string? Actor = null,
    string? Tenant = null,
    int? Ring = null,
    WorkflowEventDetail? Detail = null,
    string? Causation = null);
