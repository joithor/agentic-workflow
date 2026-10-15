namespace Aw.Sindri.Ui.Api;

/// <summary>A command the API accepted (API spec §2.1): its id, and the ledger sequence the app sends as X-Sindri-After next.</summary>
/// <param name="CommandId">The attempt's id.</param>
/// <param name="Sequence">The domain event's ledger sequence (the read-your-writes token).</param>
internal sealed record CommandAcceptedView(string CommandId, long Sequence);
