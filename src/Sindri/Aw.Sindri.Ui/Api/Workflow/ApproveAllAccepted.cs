namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The command id and ledger sequence the approval was accepted under (the same two fields every accepted command carries).</summary>
/// <param name="CommandId">The attempt's id.</param>
/// <param name="Sequence">The domain event's ledger sequence.</param>
internal sealed record ApproveAllAccepted(string CommandId, long Sequence);
