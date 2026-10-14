using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Items;

/// <summary>The action ids an item offers in a state (API spec §3.6): the UI intersects them with the command catalog.</summary>
public static class ItemActions
{
    /// <summary>The actions an item in <paramref name="state"/> offers: closing while it is new, queued or needs a decision; none once closed.</summary>
    /// <param name="state">The item's state.</param>
    /// <returns>The action ids.</returns>
    public static IReadOnlyList<ActionId> For(ItemState state)
        => throw new NotSupportedException("ItemActions.For: P3 unit D makes Actions_by_state pass");

    /// <summary>The row's primary action (UI spec §4.5): a member of <see cref="For"/>, or null (null for every P3 state).</summary>
    /// <param name="state">The item's state.</param>
    /// <returns>The action id, or null.</returns>
    public static ActionId? Primary(ItemState state)
        => throw new NotSupportedException("ItemActions.Primary: P3 unit D makes Actions_by_state pass");
}
