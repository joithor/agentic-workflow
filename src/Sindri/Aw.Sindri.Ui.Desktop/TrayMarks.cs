using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The tray's icons (UI spec §5), drawn from Sindri's mark, the ◆ of the top bar's brand (§3.2): filled for Neutral,
/// hollow for Stale, hollow and struck through for NotRunning. On macOS they are template images, so the menu bar tints them and
/// only their shape counts; elsewhere they are white, the tray convention. (The theme's Ink would need the app's
/// resources, and a head references no Application: ARCH-UI-001.) Drawn on the UI thread, once per state.
/// </summary>
internal static class TrayMarks
{
    private const int Pixels = 32;
    private const double Units = 12;
    private static readonly Geometry _mark = Geometry.Parse("M6 1.25 10.75 6 6 10.75 1.25 6z");
    private const double GlyphWidth = 2.6;
    private const double GlyphHeight = 5;
    private const double GlyphGap = 0.6;
    private const double GlyphTop = 6.5;

    // Theme/Tokens.axaml Color.Attention: a head can't read the app's resources (ARCH-UI-001), so the test
    // Attention_mark_uses_the_attention_token ties this copy to the token.
    private static readonly Color _attentionInk = Color.Parse("#f2994a");

    // The segments (a top, b upper right, c lower right, d bottom, e lower left, f upper left, g middle) lit for 0 to 9.
    private static readonly string[] _segments = ["abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgedc", "abc", "abcdefg", "abcdfg"];
    private static readonly Dictionary<(TrayState, int), WindowIcon> _icons = [];

    /// <summary>The icon for a state.</summary>
    /// <param name="state">The state.</param>
    /// <param name="count">The count an Attention or Calm icon carries.</param>
    /// <returns>The icon.</returns>
    internal static WindowIcon For(TrayState state, int count)
    {
        if (!_icons.TryGetValue((state, count), out var icon))
        {
#pragma warning disable CA2000 // the icons are cached for the life of the process
            icon = new WindowIcon(Draw(state, count));
#pragma warning restore CA2000
            _icons[(state, count)] = icon;
        }

        return icon;
    }

    /// <summary>Draws the icon for a state.</summary>
    /// <param name="state">The state.</param>
    /// <param name="count">The count an Attention or Calm icon carries (nine and over draw as "9+"); other states draw none.</param>
    /// <returns>The icon's bitmap.</returns>
    internal static Bitmap Draw(TrayState state, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var counted = state is TrayState.Attention or TrayState.Calm && count > 0;
        var ink = new SolidColorBrush(state == TrayState.Attention ? _attentionInk : Colors.White);
        var pen = new Pen(ink, thickness: 1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var bitmap = new RenderTargetBitmap(new PixelSize(Pixels, Pixels), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(Pixels / Units, Pixels / Units)))
        {
            // With a count the mark shrinks into the upper left, leaving the lower right for the digits.
            using (context.PushTransform(counted ? Matrix.CreateScale(0.75, 0.75) : Matrix.Identity))
            {
                if (state is TrayState.Neutral or TrayState.Attention or TrayState.Calm)
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

            if (counted)
            {
                DrawCount(context, pen, count);
            }
        }

        return bitmap;
    }

    // The count as seven-segment glyphs (no fonts: a headless or tray renderer draws the same), "9+" from ten.
    private static void DrawCount(DrawingContext context, Pen pen, int count)
    {
        var glyphs = count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var x = Units - GlyphGap - (glyphs.Length * GlyphWidth) - ((glyphs.Length - 1) * GlyphGap);
        foreach (var glyph in glyphs)
        {
            DrawGlyph(context, pen, glyph, x);
            x += GlyphWidth + GlyphGap;
        }
    }

    private static void DrawGlyph(DrawingContext context, Pen pen, char glyph, double x)
    {
        double left = x + 0.5, right = x + GlyphWidth - 0.5, top = GlyphTop + 0.5, middle = GlyphTop + (GlyphHeight / 2), bottom = GlyphTop + GlyphHeight - 0.5;
        if (glyph == '+')
        {
            context.DrawLine(pen, new Point(left, middle), new Point(right, middle));
            context.DrawLine(pen, new Point((left + right) / 2, middle - 1.1), new Point((left + right) / 2, middle + 1.1));
            return;
        }

        var on = _segments[glyph - '0'];
        (char Segment, Point From, Point To)[] lines =
        [
            ('a', new Point(left, top), new Point(right, top)),
            ('b', new Point(right, top), new Point(right, middle)),
            ('c', new Point(right, middle), new Point(right, bottom)),
            ('d', new Point(left, bottom), new Point(right, bottom)),
            ('e', new Point(left, middle), new Point(left, bottom)),
            ('f', new Point(left, top), new Point(left, middle)),
            ('g', new Point(left, middle), new Point(right, middle)),
        ];
        foreach (var (segment, from, to) in lines)
        {
            if (on.Contains(segment, StringComparison.Ordinal))
            {
                context.DrawLine(pen, from, to);
            }
        }
    }
}
