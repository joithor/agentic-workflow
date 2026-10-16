using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.ReadLedger;

/// <summary>One page of public events, oldest first.</summary>
/// <param name="Events">The events.</param>
/// <param name="Next">The cursor for the next page, or null when the ledger has nothing more.</param>
public sealed record LedgerPage(IReadOnlyList<PublicEvent> Events, long? Next);
