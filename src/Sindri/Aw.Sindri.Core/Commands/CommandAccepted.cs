using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>A command was accepted (API spec §2.1): its id, and the ledger sequence its domain event was written at (the read-your-writes token).</summary>
/// <param name="CommandId">The attempt's id.</param>
/// <param name="Sequence">The domain event's ledger sequence.</param>
public sealed record CommandAccepted(CommandId CommandId, long Sequence);
