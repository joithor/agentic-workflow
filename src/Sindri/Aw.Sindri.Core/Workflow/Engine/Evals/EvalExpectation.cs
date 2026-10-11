using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>What an eval line expects after its trigger (spec §9.2): the state status shows, the park reason, and the outcome once terminal.</summary>
/// <param name="State">The state ('checks', 'parked', 'done' …).</param>
/// <param name="Park">The park reason, when the state is 'parked'.</param>
/// <param name="Outcome">The terminal outcome, when the instance ended.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalExpectation(string State, ParkReason? Park, OutcomeName? Outcome);
