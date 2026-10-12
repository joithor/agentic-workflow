namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The ledger payload of a mirrored workflow event (spike: the trigger and the state change; P4 adds rev 7's full payload).</summary>
/// <param name="Instance">The instance address.</param>
/// <param name="From">The state left.</param>
/// <param name="To">The state entered.</param>
/// <param name="Trigger">The trigger's kind.</param>
/// <param name="Outcome">The trigger's outcome.</param>
/// <param name="Run">The run token.</param>
internal sealed record WorkflowEventPayload(string Instance, string From, string To, string Trigger, string? Outcome, string? Run);
