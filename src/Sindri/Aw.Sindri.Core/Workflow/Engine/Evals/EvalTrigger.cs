using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// An eval line's trigger (spec §9.2): its kind is a trigger name ('TriggerNames': 'step.completed',
/// 'step.failed', 'wait.satisfied', 'command.&lt;verb&gt;', 'child.parked', 'child.resumed', 'child.escalated'),
/// and its outcome and outputs come from the trigger's source. The runner supplies the run token, the wait's source
/// and the digests. A child's end is 'wait.satisfied' with the child's outcome (§5.7); a child's park is
/// 'child.parked' with the child's reason in <see cref="Park"/>, spelled as 'expect.park' spells it.
/// </summary>
/// <param name="Kind">The trigger's name.</param>
/// <param name="Outcome">The source's outcome.</param>
/// <param name="Outputs">The facts the source records, with their content.</param>
/// <param name="Park">The child's park reason, for 'child.parked' only.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalTrigger(string Kind, OutcomeName? Outcome, IReadOnlyDictionary<FactKey, JsonElement>? Outputs, ParkReason? Park);
