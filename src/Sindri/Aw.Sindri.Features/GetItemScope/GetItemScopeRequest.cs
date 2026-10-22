using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetItemScope;

/// <summary>An item's starting points for the Context page (GET /sindri/items/{itemId}/scope).</summary>
/// <param name="Item">The item, by id or alias.</param>
public sealed record GetItemScopeRequest(ItemRef Item) : IQuery<ItemScopeResponse>;
