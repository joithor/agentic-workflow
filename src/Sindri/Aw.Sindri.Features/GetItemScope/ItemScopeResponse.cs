using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetItemScope;

/// <summary>An item's scope as {item, scope}; scope is null when the item has no frozen scoping packet.</summary>
/// <param name="Item">The item.</param>
/// <param name="Scope">The scope read from the item's latest frozen scoping packet, or null.</param>
public sealed record ItemScopeResponse(ItemId Item, ItemScope? Scope);
