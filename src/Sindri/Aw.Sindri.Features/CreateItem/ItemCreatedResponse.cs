using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>The created item (API spec §3.2 'ItemCreated(accepted, ItemId)'): '{accepted:{commandId, sequence}, itemId}'.</summary>
/// <param name="Accepted">The command and the ledger sequence of its 'item.created'.</param>
/// <param name="ItemId">The new item's id.</param>
public sealed record ItemCreatedResponse(CommandAccepted Accepted, ItemId ItemId);
