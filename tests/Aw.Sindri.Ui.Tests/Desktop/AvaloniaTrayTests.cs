using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>What the Avalonia tray draws and wires: escaped menu labels, the menu's two events, one icon per state.</summary>
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

    // The bitmap's encoded pixels, so two drawings compare by content.
    private static string Pixels(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return Convert.ToHexString(stream.ToArray());
    }
}
