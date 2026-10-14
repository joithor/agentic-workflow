using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Maps an items-view row to the summary the queue and the list share.</summary>
public static class ItemSummaries
{
    /// <summary>The summary of <paramref name="row"/>, with its actions from ItemActions.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The summary.</returns>
    public static ItemSummary From(ItemRow row)
        => throw new NotSupportedException("ItemSummaries.From: P3 unit G makes Queue_returns_every_group_even_empty pass");
}
