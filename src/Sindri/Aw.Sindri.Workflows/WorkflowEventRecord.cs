namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// One event the interpreter mirrors into the ledger through <see cref="RecordEventActivities"/> (engine spec rev 8 §5.6).
/// <see cref="Sequence"/> is the workflow's own count of the events it mirrored: workflow state, rebuilt by replay, so a
/// replayed or retried record has the same (workflow id, run id, sequence) and so the same idempotency key.
/// </summary>
/// <param name="WorkflowId">The workflow id: the instance address.</param>
/// <param name="RunId">The workflow run id.</param>
/// <param name="Sequence">The mirrored event's position in this run, from 1.</param>
/// <param name="Type">'workflow.started' or 'workflow.transition'.</param>
/// <param name="Item">The work item.</param>
/// <param name="From">The state left ('' for a start).</param>
/// <param name="To">The state entered.</param>
/// <param name="Trigger">The trigger's kind ('start', 'step.completed', 'timeout').</param>
/// <param name="Outcome">The trigger's outcome, when it has one.</param>
/// <param name="Run">The run token the trigger reported on, when it has one.</param>
internal sealed record WorkflowEventRecord(
    string WorkflowId,
    string RunId,
    int Sequence,
    string Type,
    string Item,
    string From,
    string To,
    string Trigger,
    string? Outcome,
    string? Run)
{
    /// <summary>The ledger idempotency key: 'workflow:{workflow id}:{run id}:{sequence}'.</summary>
    public string IdempotencyKey => $"workflow:{WorkflowId}:{RunId}:{Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
