using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The pure decisions of the item commands (API spec §2.1 step 2).</summary>
public static class ItemDecisions
{
    /// <summary>Closes <paramref name="item"/> for <paramref name="reason"/> when it is still at the version the human saw: SND-ITEM-409 when it changed since, or is already closed.</summary>
    /// <param name="item">The item as the views hold it.</param>
    /// <param name="reason">Done or cancelled.</param>
    /// <param name="seenVersion">The version the human saw.</param>
    /// <param name="command">The 'CloseItem' command's id.</param>
    /// <returns>The state change to append, or SND-ITEM-409.</returns>
    public static Result<ItemStateChanged> Close(ItemRow item, CloseReason reason, long seenVersion, CommandId command)
        => throw new NotSupportedException("ItemDecisions.Close: P3 unit D makes Close_at_the_seen_version_changes_state pass");
}
