using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>A workflow ledger row as the engine mirrors it: what the real ledger stores, and (<see cref="ToEvent"/>) what the fold reads.</summary>
/// <param name="Type">The ledger event type.</param>
/// <param name="At">When it was recorded.</param>
/// <param name="Actor">The envelope's actor.</param>
/// <param name="Payload">The workflow payload.</param>
public sealed record StepRunRow(string Type, DateTimeOffset At, string Actor, WorkflowEventPayload Payload)
{
    /// <summary>The event the fold reads for this row.</summary>
    /// <returns>The event.</returns>
    public RecordedWorkflowEvent ToEvent() => StepRunEventMap.Of(Type, At, Actor, Payload);

    public static implicit operator RecordedWorkflowEvent(StepRunRow row) => row.ToEvent();
}
