using Aw.Sindri.Core.Workflow.Engine.Events;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>'recipe eval --record' (spec §6.3, §9.2): a live instance's history written as an eval case. P3 unit (f) defines the format; the query lands with the store (P4).</summary>
public static class EvalRecorder
{
    /// <summary>The case that replays <paramref name="events"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="events">The instance's events, oldest first, from its workflow.started.</param>
    /// <returns>The case.</returns>
    public static EvalCase Record(string name, IReadOnlyList<WorkflowEvent> events) =>
        throw new NotSupportedException("P3 unit (f): EvalRecorder.Record");
}
