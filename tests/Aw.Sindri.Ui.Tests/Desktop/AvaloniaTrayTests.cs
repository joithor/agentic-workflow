using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>
/// What the Avalonia tray draws and wires: escaped menu labels, the menu's two events, one visible icon per state, and
/// the icon registered with the app (Avalonia realises a TrayIcon only once it is in the Application's TrayIcon.Icons).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class AvaloniaTrayTests
{
    private static readonly TrayView _neutral = new(TrayState.Neutral, "Sindri · running", "Open Sindri", "Quit");

    private static async Task OnUi(Action body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AvaloniaTrayTests).Assembly)
            .Dispatch(() =>
            {
                body();
                return true;
            }, TestContext.Current.CancellationToken);

    private static NativeMenuItem Item(AvaloniaTray tray, int index) => tray.Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).ElementAt(index);

    [Fact]
    public Task Menu_labels_escape_underscores_and_the_tooltip_is_as_given() => OnUi(() =>
    {
        var tray = new AvaloniaTray();

        tray.Show(_neutral with { Tooltip = "Sindri · repo_name", OpenLabel = "Open_Sindri", QuitLabel = "Quit_" });

        Assert.Equal("Sindri · repo_name", tray.Icon!.ToolTipText);
        Assert.Equal("Open__Sindri", Item(tray, 0).Header);
        Assert.Equal("Quit__", Item(tray, 1).Header);
        Assert.True(tray.Icon.IsVisible);
    });

    [Fact]
    public Task The_menu_raises_open_and_quit() => OnUi(() =>
    {
        var tray = new AvaloniaTray();
        var (opened, quit) = (0, 0);
        tray.OpenRequested += (_, _) => opened++;
        tray.QuitRequested += (_, _) => quit++;
        tray.Show(_neutral);

        Item(tray, 0).Command!.Execute(parameter: null);
        Item(tray, 1).Command!.Execute(parameter: null);

        Assert.Equal((1, 1), (opened, quit));
    });

    [Fact]
    public Task Each_state_has_its_own_icon() => OnUi(() =>
    {
        var frames = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "frames")).FullName;
        var pixels = Enum.GetValues<TrayState>().Select(state =>
        {
            var icon = TrayMarks.Draw(state);
            icon.Save(Path.Combine(frames, $"tray-{state}.png"), PngBitmapEncoderOptions.Default);
            return Pixels(icon);
        }).ToList();

        Assert.Equal(3, pixels.Distinct(StringComparer.Ordinal).Count());
    });

    [Fact]
    public Task Show_registers_the_icon_with_the_app_so_the_platform_realises_it() => OnUi(() =>
    {
        var tray = new AvaloniaTray();

        tray.Show(_neutral);
        tray.Show(_neutral with { State = TrayState.Stale });

        _ = Assert.Single(TrayIcon.GetIcons(Application.Current!)!, icon => ReferenceEquals(icon, tray.Icon));
    });

    [Fact]
    public Task Quit_removes_the_icon_from_the_app() => OnUi(() =>
    {
        var tray = new AvaloniaTray();
        tray.Show(_neutral);
        var icon = tray.Icon!;

        Item(tray, 1).Command!.Execute(parameter: null);

        Assert.DoesNotContain(icon, TrayIcon.GetIcons(Application.Current!) ?? []);
    });

    [Theory]
    [InlineData(TrayState.Neutral)]
    [InlineData(TrayState.NotRunning)]
    [InlineData(TrayState.Stale)]
    internal Task Each_state_draws_a_visible_mark(TrayState state) => OnUi(() =>
    {
        var icon = TrayMarks.Draw(state);

        // The mark covers a good part of the 32 px square (a hollow one at least its outline), not a blank image.
        Assert.InRange(Opaque(icon), 60, (32 * 32) - 1);
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
}
