using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Replay (spec W21, §10 'Replay reproduces the instance'): every event carries the full post-state delta, so folding
/// an instance's events from its workflow.started rebuilds the row exactly. P3 unit (e).
/// </summary>
public static class EventFold
{
    /// <summary>The delta from <paramref name="before"/> to <paramref name="after"/>: each changed part's full post-state value.</summary>
    /// <param name="before">The row before the Commit; null for workflow.started.</param>
    /// <param name="after">The row after it.</param>
    /// <returns>The delta.</returns>
    public static InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after) =>
        throw new NotSupportedException("P3 unit (e): EventFold.Delta");

    /// <summary>Applies one event: workflow.started yields its row; every other event applies its delta to <paramref name="before"/>.</summary>
    /// <param name="before">The row so far; null before workflow.started.</param>
    /// <param name="recorded">The event.</param>
    /// <returns>The row after it.</returns>
    public static WorkflowInstance Apply(WorkflowInstance? before, WorkflowEvent recorded) =>
        throw new NotSupportedException("P3 unit (e): EventFold.Apply");

    /// <summary>Folds an instance's events, oldest first, from its workflow.started.</summary>
    /// <param name="events">The events.</param>
    /// <returns>The row they rebuild.</returns>
    public static WorkflowInstance Fold(IEnumerable<WorkflowEvent> events) =>
        throw new NotSupportedException("P3 unit (e): EventFold.Fold");
}
