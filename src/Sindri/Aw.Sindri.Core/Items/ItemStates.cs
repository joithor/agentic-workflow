using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Items;

/// <summary>Pure questions about an item state.</summary>
public static class ItemStates
{
    /// <summary>True when the item is closed ('done' or 'cancelled').</summary>
    /// <param name="state">The state.</param>
    /// <returns>True when closed.</returns>
    public static bool IsClosed(ItemState state)
        => throw new NotSupportedException("ItemStates.IsClosed: P3 unit D makes it pass");
}
