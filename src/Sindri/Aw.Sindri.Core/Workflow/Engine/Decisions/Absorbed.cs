namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// The trigger was taken without changing the instance (spec §5.7, §5.8, UI E-6): an effect refusal the same revoke's
/// park absorbs, a drift refusal while already parked for drift, a child's change nothing waits on, or an
/// open-request on an instance already taken over. Nothing goes to the ledger.
/// </summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Reason">Why nothing changed, in the words status and the launcher show.</param>
public sealed record Absorbed(TriggerKey Key, string Reason) : Decision(Key);
