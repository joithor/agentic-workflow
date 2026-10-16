using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A row's own buttons act (real-app re-proof, NB1): clicking or pressing Space on a row's own button sends the action.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouActionButtonTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouActionButtonTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int n)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        shell.ShowNeedsYou(new NeedsYouFeed([.. Enumerable.Range(0, n).Select(i => NeedsYouRows.Decision("decision:DEC-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))], ""));
        Settle(window);
        return (window, shell);
    }

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];


    private static Button Accept(NeedsYouRow row) =>
        row.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, "Accept", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public Task A_click_on_a_rows_own_option_button_sends_that_option_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Accept(Rows(window)[0]);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100, Token);
        Settle(window);

        var (decision, _) = Assert.Single(_server.DecideCalls);
        Assert.Equal("DEC-1", decision.Replace("decision:", "", StringComparison.Ordinal));
    });

    [Fact]
    public Task Space_on_the_focused_option_button_sends_that_option_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Accept(Rows(window)[0]);
        Assert.True(button.Focus(NavigationMethod.Tab));
        Settle(window);

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        await Task.Delay(100, Token);
        Settle(window);

        _ = Assert.Single(_server.DecideCalls);
    });
}
