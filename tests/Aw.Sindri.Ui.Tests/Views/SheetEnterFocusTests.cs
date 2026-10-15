using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// UI spec §11.2 with the sheet beside the page: Enter again on the row it shows moves focus to the sheet's first action,
/// and the focused row's ring stays off the sheet (proof B2).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SheetEnterFocusTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SheetEnterFocusTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OpenSheet(double width)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = width };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);
        shell.GoTo(PageId.Queue);
        Settle(window);
        for (var i = 0; i < 12 && shell.Queue.SelectedId != A.NewItem; i++)
        {
            window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        }

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Until(window, () => shell.IsSheetShown && shell.Item.HasItem && shell.Item.Actions.Count > 0);
        return (window, shell);
    }

    private static async Task Until(Window window, Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), "the view never reached the expected state");
    }

    private static bool InFirstAction(Window window)
    {
        var list = window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "ItemActionList");
        var first = list.GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton");
        return ReferenceEquals(window.FocusManager?.GetFocusedElement(), first);
    }

    [Fact]
    public Task Enter_again_beside_the_page_focuses_the_sheets_first_action() => OnUi(async () =>
    {
        var (window, _) = await OpenSheet(width: 1400);
        Assert.False(InFirstAction(window));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window);

        Assert.True(InFirstAction(window));
    });

    [Fact]
    public Task The_focused_rows_ring_is_not_drawn_over_the_open_sheet() => OnUi(async () =>
    {
        var (window, _) = await OpenSheet(width: 1400);
        var row = window.FocusManager?.GetFocusedElement() as Control;
        Assert.NotNull(row);

        var ring = AdornerLayer.GetAdornerLayer(row)!.Children.OfType<Border>().Single(b => b.IsEffectivelyVisible && AdornerLayer.GetAdornedElement(b) == row);
        var sheet = window.Get<Border>("Sheet");
        var ringRight = ring.TranslatePoint(new Point(ring.Bounds.Width, 0), window)!.Value.X;
        var sheetLeft = sheet.TranslatePoint(default, window)!.Value.X;

        Assert.True(ringRight <= sheetLeft + 0.5, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the ring ends at {ringRight}, the sheet starts at {sheetLeft}"));
    });
}
