using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>One queue group: '{group, items, empty}' (U2-API-2).</summary>
/// <param name="Group">The group.</param>
/// <param name="Items">Its items, the oldest 'since' first.</param>
/// <param name="Empty">The group's own sentence when it has no item, else null.</param>
public sealed record QueueGroupView(QueueGroup Group, IReadOnlyList<ItemSummary> Items, string? Empty);
