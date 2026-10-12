using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --record' (spec §6.3, §9.2): a live instance's history written as an eval case. The events give the
/// triggers, gate verdicts and post-states (folded with <see cref="EventFold"/>); a line's outputs are the facts its
/// trigger reported, as the store saved them, and their content comes from the store's facts, never from the events,
/// which hold only references (an event's delta leaves out a fact reported again unchanged). The query against serve
/// lands with the store (P4).
/// </summary>
public static class EvalRecorder
{
    /// <summary>The case that replays <paramref name="events"/>.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="events">The instance's events, oldest first, from its workflow.started.</param>
    /// <param name="reported">The facts each trigger reported, by its key, as the store saved them; a trigger missing here gives the facts its event's delta added.</param>
    /// <param name="contents">The store's fact content, by <see cref="FactValue.Ref"/>.</param>
    /// <returns>The case, or why the history can't be one: a broken log, a missing fact's content, or a trigger an eval line can't name.</returns>
    public static Result<EvalCase> Record(
        string name,
        IReadOnlyList<WorkflowEvent> events,
        IReadOnlyDictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>> reported,
        IReadOnlyDictionary<string, JsonElement> contents) =>
        throw new NotSupportedException("P3 unit (f): EvalRecorder.Record");
}
