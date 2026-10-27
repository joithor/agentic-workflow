namespace Aw.Sindri.Core.ModelHistory;

/// <summary>An escalation a 'model.picked' records: the model that failed at a state, and how.</summary>
/// <param name="Instance">The instance, as an address or an id.</param>
/// <param name="State">The state.</param>
/// <param name="From">The model that was escalated from.</param>
/// <param name="Outcome">How its run ended: refused or check_failed.</param>
internal sealed record EscalatedPick(string Instance, string State, string From, ModelOutcome Outcome);
