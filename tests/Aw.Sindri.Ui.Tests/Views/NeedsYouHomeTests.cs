using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Home's Needs you panel under the headless platform (U3' T9): rows with the one chip, the summary line, the overflow, the sheets.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouHomeTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouHomeTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(double? width = null)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        if (width is { } w)
        {
            window.Width = w;
        }

        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int count)
    {
        var (window, shell) = await Connected();
        shell.ShowNeedsYou(new ViewModels.NeedsYou.NeedsYouFeed([.. Enumerable.Range(0, count).Select(i => NeedsYouRows.Park($"park:ITM-{i}"))], ""));
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task Home_draws_the_served_rows_in_place_of_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(3);

        Assert.Equal(3, Rows(window).Count);
        Assert.DoesNotContain(Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == Labels.NotServedYet);
        Assert.Equal(shell.Attention.SummaryLine, Named<TextBlock>(window, "NeedsYouSummary").Text);
    });

    [Fact]
    public Task A_row_draws_its_chip_with_the_one_chip_control() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);

        var chip = Rows(window)[0].GetVisualDescendants().OfType<StateChipView>().Single();

        Assert.True(chip.IsEffectivelyVisible);
        Assert.Equal("PARKED", chip.GetVisualDescendants().OfType<TextBlock>().First().Text);
    });

    [Fact]
    public Task Seven_rows_show_five_then_Show_all_expands_in_place() => OnUi(async () =>
    {
        var (window, _) = await WithRows(7);
        Assert.Equal(5, Rows(window).Count);

        var showAll = Named<Button>(window, "NeedsYouShowAll");
        Assert.True(showAll.IsEffectivelyVisible);
        showAll.Command?.Execute(null);
        showAll.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(7, Rows(window).Count);
        Assert.False(showAll.IsEffectivelyVisible);
    });

    [Fact]
    public Task A_needs_you_link_shows_the_row_sheet_not_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "park:ITM-1"));
        Settle(window);

        Assert.True(Named<Control>(window, "NeedsYouSheet").IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "SheetBody").IsEffectivelyVisible);
    });

    [Fact]
    public Task Clicking_a_rows_title_selects_it_and_opens_its_sheet() => OnUi(async () =>
    {
        var (window, shell) = await WithRows(2);

        Named<Button>(Rows(window)[1], "NeedsYouTitle").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(("park:ITM-1", true), (shell.NeedsYou.SelectedId, shell.IsSheetOpen));
    });
}
