using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'item.state-changed' payload (API spec §3.4): an item moved from one state to another, at a new version.</summary>
/// <param name="CommandId">The command that moved it.</param>
/// <param name="Item">The item.</param>
/// <param name="From">Its state before.</param>
/// <param name="To">Its state after.</param>
/// <param name="Version">Its version after the change (the fact key's 'v&lt;version&gt;').</param>
/// <param name="Reason">Why, when the command gave one, or null.</param>
public sealed record ItemStateChanged(CommandId CommandId, ItemId Item, ItemState From, ItemState To, long Version, string? Reason);
