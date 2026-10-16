using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

    [Fact]
    public Task Show_lists_the_rows_then_open_running_quit_and_picking_a_row_raises_only_that() => OnUi(() =>
    {
        var tray = new AvaloniaTray();
        var rows = new List<string>();
        tray.RowRequested += (_, e) => rows.Add(e.RowId);
        tray.Show(_neutral with { State = TrayState.Attention, Count = 2, Rows = [new TrayRow("a", "A"), new TrayRow("b", "B")], RunningLabel = "Running: 1" });

        var items = tray.Menu!.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).ToList();
        items[1].Command!.Execute(parameter: null);

        Assert.Equal(["A", "B", "Open Sindri", "Running: 1", "Quit"], items.Select(i => i.Header), StringComparer.Ordinal);
        Assert.False(items[3].IsEnabled);
        Assert.Equal(["b"], rows);
    });
}
