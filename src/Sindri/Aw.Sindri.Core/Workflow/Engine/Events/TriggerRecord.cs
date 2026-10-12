using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>The trigger an event records (spec §5.6): its kind, its id (the key) and its actor, and the source's outcome.</summary>
/// <param name="Kind">The trigger's name ('step.completed', 'answer', 'deadline' …).</param>
/// <param name="Key">'wf:{instance}:{trigger}'.</param>
/// <param name="Actor">Who fired it.</param>
/// <param name="Outcome">The source's outcome, for a Step report or a satisfied wait.</param>
public sealed record TriggerRecord(string Kind, TriggerKey Key, string Actor, OutcomeName? Outcome);
