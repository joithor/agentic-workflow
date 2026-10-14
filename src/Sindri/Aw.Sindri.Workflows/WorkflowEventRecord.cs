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
    string? Causation = null);
