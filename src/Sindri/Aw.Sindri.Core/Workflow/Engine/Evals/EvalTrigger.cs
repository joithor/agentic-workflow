using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// An eval line's trigger (spec §9.2): its kind is a trigger name ('TriggerNames': 'step.completed',
/// 'step.failed', 'wait.satisfied', 'command.&lt;verb&gt;', 'child.parked', 'child.resumed', 'child.escalated', 'answer',
/// 'takeover', 'handback', 'cancel', 'deadline'), with <see cref="Variant"/> and <see cref="Amount"/> for the engine triggers,
/// and its outcome and outputs come from the trigger's source. The runner supplies the run token, the wait's source
/// and the digests. A child's end is 'wait.satisfied' with the child's outcome (§5.7); a child's park is
/// 'child.parked' with the child's reason in <see cref="Park"/>, spelled as 'expect.park' spells it.
/// </summary>
/// <param name="Kind">The trigger's name.</param>
/// <param name="Outcome">The source's outcome.</param>
/// <param name="Outputs">The facts the source records, with their content.</param>
/// <param name="Park">The child's park reason, for 'child.parked' only.</param>
/// <param name="Variant">The engine trigger the line stands for: required with kind 'answer' (its option), optional and equal to the kind otherwise.</param>
/// <param name="Amount">The n of '--grant n' or '--grant-tokens n'.</param>
/// <param name="Reason">A step.failed's reason, for 'step.failed' only; left out, the runner uses a constant, so two such lines are the same failure ('sameFailure', §3.7).</param>
/// <param name="Tokens">The tokens the run reports, for 'step.completed' and 'step.failed' only; left out, the run spent none, so a budget never binds in an eval unless a line says it spent.</param>
/// <param name="Code">The code of an infrastructure failure, for 'step.failed' only (an unavailable dependency); it becomes the park's code once the retries are used up.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalTrigger(
    string Kind,
    OutcomeName? Outcome = null,
    IReadOnlyDictionary<FactKey, JsonElement>? Outputs = null,
    ParkReason? Park = null,
    EvalVariant? Variant = null,
    int? Amount = null,
    string? Reason = null,
    int? Tokens = null,
    string? Code = null);
