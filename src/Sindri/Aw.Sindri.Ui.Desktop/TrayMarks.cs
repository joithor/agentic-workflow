using Avalonia.Media.Imaging;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The tray's icons: Sindri's mark filled (Neutral), hollow (Stale), and hollow and struck through (NotRunning).</summary>
internal static class TrayMarks
{
    /// <summary>Draws the icon for a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The icon's bitmap.</returns>
    internal static Bitmap Draw(TrayState state) => throw new NotSupportedException($"{state}");
}
