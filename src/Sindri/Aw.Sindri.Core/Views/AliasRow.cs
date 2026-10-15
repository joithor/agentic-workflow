using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'item-aliases' view, keyed by the alias: the item a tracker key resolves to.</summary>
/// <param name="Alias">The tracker key ('ENG-142').</param>
/// <param name="Item">The item it names.</param>
/// <param name="Record">The tracker record behind it.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record AliasRow(ItemAlias Alias, ItemId Item, RecordKey Record, long Sequence);
