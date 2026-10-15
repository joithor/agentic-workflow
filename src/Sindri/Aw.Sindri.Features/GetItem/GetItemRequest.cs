using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetItem;

/// <summary>One item with its version and actions (GET /sindri/items/{itemId}; API spec §3.1).</summary>
/// <param name="Item">The item, by id or alias.</param>
public sealed record GetItemRequest(ItemRef Item) : IQuery<ItemView>;
