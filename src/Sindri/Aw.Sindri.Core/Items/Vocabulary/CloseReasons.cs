namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>The one mapping of a <see cref="CloseReason"/>: its catalog variant and the state it closes to (an undefined value maps to neither, so no side reads it as a default).</summary>
public static class CloseReasons
{
    /// <summary>The catalog variant of 'CloseItem' ('done' or 'cancelled'), or 'undefined' for a value that isn't a reason: that has no catalog row, so it classifies protected.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The variant.</returns>
    public static string Variant(CloseReason reason) => Map(reason, out var variant, out _) ? variant : "undefined";

    /// <summary>The state an item closes to for <paramref name="reason"/>.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="state">The state: done or cancelled.</param>
    /// <returns>False when <paramref name="reason"/> isn't a defined reason.</returns>
    public static bool TryTarget(CloseReason reason, out ItemState state) => Map(reason, out _, out state);

    private static bool Map(CloseReason reason, out string variant, out ItemState state)
    {
        switch (reason)
        {
            case CloseReason.Done:
                (variant, state) = ("done", ItemState.Done);
                return true;
            case CloseReason.Cancelled:
                (variant, state) = ("cancelled", ItemState.Cancelled);
                return true;
            default:
                (variant, state) = (string.Empty, default);
                return false;
        }
    }
}
