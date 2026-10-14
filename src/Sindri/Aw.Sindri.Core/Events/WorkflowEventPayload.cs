using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The ledger payload of a mirrored workflow event (<see cref="EventTypes.WorkflowStarted"/>,
/// <see cref="EventTypes.WorkflowTransition"/>; engine spec rev 8.1 §5.6). P4a: the trigger and the state change; P4 adds
/// rev 7's full payload.
/// </summary>
/// <param name="Instance">The instance address.</param>
/// <param name="From">The state left (empty for a start).</param>
/// <param name="To">The state entered.</param>
/// <param name="Trigger">The trigger's kind.</param>
/// <param name="Outcome">The trigger's outcome.</param>
/// <param name="Run">The run token.</param>
/// <param name="Effects">The identities of the effects the change runs (§5.6, §5.7; engine P6); left out when it runs none.</param>
public sealed record WorkflowEventPayload(
    string Instance,
    string From,
    string To,
    string Trigger,
    string? Outcome,
    string? Run,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Effects = null);
