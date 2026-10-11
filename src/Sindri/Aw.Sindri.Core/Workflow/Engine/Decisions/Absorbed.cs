namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// The trigger was taken without changing the row (spec §5.1, §5.4, UI E-6): an 'outbox.refused' the same revoke's
/// park absorbs, a drift refusal while already parked for drift, or an open-request on an instance already taken
/// over. Its key is recorded, so whoever waits on it sees the result.
/// </summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Reason">Why nothing changed, in the words status and the launcher show.</param>
public sealed record Absorbed(TriggerKey Key, string Reason) : Decision(Key);
