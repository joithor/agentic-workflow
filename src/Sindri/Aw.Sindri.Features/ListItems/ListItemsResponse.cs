using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListItems;

/// <summary>The item list (API spec §3.1).</summary>
/// <param name="Items">The items, the oldest 'since' first.</param>
/// <param name="Empty">The EmptyText sentence for the filters when there is none (or the views are stale), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ListItemsResponse(IReadOnlyList<ItemSummary> Items, string? Empty, AsOf AsOf);
