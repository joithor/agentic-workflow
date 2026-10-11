using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The tray's icons (UI spec §5), drawn from Sindri's mark (the top bar's brand glyph): filled for Neutral, hollow for
/// Stale, hollow and struck through for NotRunning. On macOS they are template images, so the menu bar tints them and
/// only their shape counts; elsewhere they are white, the tray convention. (The theme's Ink would need the app's
/// resources, and a head references no Application: ARCH-UI-001.) Drawn on the UI thread, once per state.
/// </summary>
internal static class TrayMarks
{
    private const int Pixels = 32;
    private const double Units = 12;
    private static readonly Geometry _mark = Geometry.Parse("M2 10.25 6 2.75l4 7.5H7.4L6 7.55 4.6 10.25z");
    private static readonly Dictionary<TrayState, WindowIcon> _icons = [];

    /// <summary>The icon for a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The icon.</returns>
    internal static WindowIcon For(TrayState state)
    {
        if (!_icons.TryGetValue(state, out var icon))
        {
            icon = new WindowIcon(Draw(state));
            _icons[state] = icon;
        }

        return icon;
    }

    /// <summary>Draws the icon for a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The icon's bitmap.</returns>
    internal static Bitmap Draw(TrayState state)
    {
        var ink = new SolidColorBrush(Colors.White);
        var pen = new Pen(ink, thickness: 1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var bitmap = new RenderTargetBitmap(new PixelSize(Pixels, Pixels), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(Pixels / Units, Pixels / Units)))
        {
            if (state == TrayState.Neutral)
            {
                context.DrawGeometry(ink, pen: null, _mark);
            }
            else
            {
                context.DrawGeometry(brush: null, pen, _mark);
            }

            if (state == TrayState.NotRunning)
            {
                context.DrawLine(pen, new Point(1.5, 11), new Point(10.5, 1.5));
            }
        }

        return bitmap;
    }
}
