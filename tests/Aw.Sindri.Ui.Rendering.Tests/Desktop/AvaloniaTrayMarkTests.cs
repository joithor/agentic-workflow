using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Rendering.Tests.Desktop;

/// <summary>What the Avalonia tray draws, read back from its pixels: one visible icon per state, the attention token, the count.</summary>
[Lane(TestLane.Integration)]
public sealed class AvaloniaTrayMarkTests
{
    private static async Task OnUi(Action body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTrayMarkTests).Assembly)
            .Dispatch(() =>
            {
                body();
                return true;
            }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Each_state_has_its_own_icon() => OnUi(() =>
    {
        var frames = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "frames")).FullName;
        var pixels = Enum.GetValues<TrayState>().Select(state =>
        {
            var icon = TrayMarks.Draw(state, count: 3);
            icon.Save(Path.Combine(frames, $"tray-{state}.png"), PngBitmapEncoderOptions.Default);
            return Pixels(icon);
        }).ToList();

        Assert.Equal(Enum.GetValues<TrayState>().Length, pixels.Distinct(StringComparer.Ordinal).Count());
    });

    [Theory]
    [InlineData(TrayState.Neutral)]
    [InlineData(TrayState.NotRunning)]
    [InlineData(TrayState.Stale)]
    [InlineData(TrayState.Attention)]
    [InlineData(TrayState.Calm)]
    internal Task Each_state_draws_a_visible_mark(TrayState state) => OnUi(() =>
    {
        var icon = TrayMarks.Draw(state, count: 3);

        // The mark covers a good part of the 32 px square (a hollow one at least its outline), not a blank image.
        Assert.InRange(Opaque(icon), 60, (32 * 32) - 1);
    });

    // The attention mark is the theme's one attention colour (Theme/Tokens.axaml Color.Attention), not the tray's white;
    // the head can't read the app's resources (ARCH-UI-001), so this ties its copy to the token.
    [Fact]
    public Task Attention_mark_uses_the_attention_token() => OnUi(() =>
    {
        var token = Color.Parse(TokenFile.Load().Color("Attention"));

        var colours = Opaque(TrayMarks.Draw(TrayState.Attention, count: 3), minAlpha: 255);

        // Anti-aliased overlaps of the mark and its digit round a channel or two; the colour itself is the token's.
        Assert.Contains(token, colours);
        Assert.All(colours, c => Assert.InRange(Math.Abs(c.R - token.R) + Math.Abs(c.G - token.G) + Math.Abs(c.B - token.B), 0, 6));
    });

    // A calm count stays monochrome: every drawn pixel is white (a template image on macOS), whatever the count.
    [Fact]
    public Task Calm_and_neutral_marks_stay_monochrome() => OnUi(() =>
    {
        foreach (var state in (TrayState[])[TrayState.Neutral, TrayState.Calm])
        {
            Assert.Equal([Colors.White], Opaque(TrayMarks.Draw(state, count: 3), minAlpha: 255));
        }
    });

    // The count is drawn: 1 to 9, and "9+" from ten, each its own image.
    [Fact]
    public Task Counts_draw_distinct_marks_and_ten_or_more_reads_9_plus() => OnUi(() =>
    {
        var pixels = Enumerable.Range(1, 9).Select(n => Pixels(TrayMarks.Draw(TrayState.Attention, n))).ToList();

        Assert.Equal(9, pixels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Pixels(TrayMarks.Draw(TrayState.Attention, 10)), Pixels(TrayMarks.Draw(TrayState.Attention, 42)));
        Assert.DoesNotContain(Pixels(TrayMarks.Draw(TrayState.Attention, 10)), pixels, StringComparer.Ordinal);
    });

    // How many of the bitmap's pixels aren't fully transparent (template images count only alpha).
    private static int Opaque(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return Enumerable.Range(0, size.Width * size.Height).Count(i => bytes[(i * 4) + 3] != 0);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // The bitmap's encoded pixels, so two drawings compare by content.
    private static string Pixels(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return Convert.ToHexString(stream.ToArray());
    }

    // The distinct colours of the bitmap's pixels at least 'minAlpha' opaque (alpha dropped, 8-bit colour).
    private static List<Color> Opaque(Bitmap bitmap, byte minAlpha)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * size.Height);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, stride * size.Height, stride);
            var bytes = new byte[stride * size.Height];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);

            // Premultiplied BGRA (or RGBA) is opaque-only here: a fully opaque pixel is the same either way up to R and B.
            return [.. Enumerable.Range(0, size.Width * size.Height)
                .Where(i => bytes[(i * 4) + 3] >= minAlpha)
                .Select(i => bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888
                    ? Color.FromRgb(bytes[i * 4], bytes[(i * 4) + 1], bytes[(i * 4) + 2])
                    : Color.FromRgb(bytes[(i * 4) + 2], bytes[(i * 4) + 1], bytes[i * 4]))
                .Distinct()];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
