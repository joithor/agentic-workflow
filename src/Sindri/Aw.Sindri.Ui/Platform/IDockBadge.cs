namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the dock or taskbar badge (UI spec §5, §18.2): the count of needs-you rows on the app's icon, none at zero. The
/// desktop head adapts it per platform (macOS NSDockTile, Windows ITaskbarList3 overlay, Linux best effort); where a
/// platform has none, <see cref="NullDockBadge"/> stands in. Called on the UI thread.
/// </summary>
public interface IDockBadge
{
    /// <summary>The count shown now; 0 when the badge is clear.</summary>
    public int Shown { get; }

    /// <summary>Shows a count, replacing the one before; 0 (or less) clears the badge.</summary>
    /// <param name="count">How many rows need the user.</param>
    public void Show(int count);
}
