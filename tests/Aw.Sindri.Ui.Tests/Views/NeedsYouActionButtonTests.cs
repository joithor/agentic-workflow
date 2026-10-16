using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
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

    private Task<(ShellWindow Window, ShellViewModel Shell)> WithRows(int n) =>
        WithFeed(new NeedsYouFeed([.. Enumerable.Range(0, n).Select(i => NeedsYouRows.Decision("decision:DEC-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))], ""));

    private async Task<(ShellWindow Window, ShellViewModel Shell)> WithFeed(NeedsYouFeed feed)
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
        shell.ShowNeedsYou(feed);
        Settle(window);
        return (window, shell);
    }

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];


    // Decline, not the first option: a button that sent the wrong option would pass with Accept.
    private static Button Decline(NeedsYouRow row) =>
        row.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, "Decline", StringComparison.OrdinalIgnoreCase));

    private async Task SentOnce(Window window)
    {
        for (var i = 0; i < 300 && _server.DecideCalls.Count == 0; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        var (decision, request) = Assert.Single(_server.DecideCalls);
        Assert.Equal("DEC-1", decision.Replace("decision:", "", StringComparison.Ordinal));
        Assert.Equal("decline", request.Choice);
    }

    [Fact]
    public Task A_click_on_a_rows_own_option_button_sends_decline_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Decline(Rows(window)[0]);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await SentOnce(window);
    });

    [Fact]
    public Task Space_on_the_focused_option_button_sends_decline_once() => OnUi(async () =>
    {
        var (window, _) = await WithRows(1);
        var button = Decline(Rows(window)[0]);
        Assert.True(button.Focus(NavigationMethod.Tab));
        Settle(window);

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        await SentOnce(window);
    });

    [Fact]
    public Task A_failure_rows_Pause_button_tooltip_help_text_and_sheet_line_carry_the_pause_command() => OnUi(async () =>
    {
        var list = AwJson.Deserialize<NeedsYouList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("needs-you", "fixture")), SindriApiClient.Json).Value;
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
        var service = list.Rows.Single(r => r.Kind == "failure").Subject!.Id;
        var pause = catalog.Rows.Single(r => r.Action == "PauseService").Cli.Replace("<name>", service, StringComparison.Ordinal);
        var feed = NeedsYouMapper.Map(list with { Rows = [list.Rows.Single(r => r.Kind == "failure")] }, catalog, decisions: null, new HomeCounts(0, 0, 0));
        var (window, _) = await WithFeed(feed);

        var row = Rows(window).Single();
        var button = row.GetVisualDescendants().OfType<Button>().First(b => string.Equals(b.Content as string, Labels.Action("PauseService"), StringComparison.Ordinal));

        Assert.Equal(pause, ToolTip.GetTip(button));
        Assert.Equal(pause, AutomationProperties.GetHelpText(button));
        var line = ((NeedsYouRowViewModel)row.DataContext!).Details.Single(d => d.Label == Labels.Action("PauseService"));
        Assert.Contains(pause, line.Value, StringComparison.Ordinal);
    });
}
