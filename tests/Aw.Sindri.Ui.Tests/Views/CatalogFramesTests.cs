using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
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
/// U1's key frames (UI spec §18.2: one per state in the integration lane), against the in-memory API serving the
/// goldens: the stalled banner with Rebuild views (a ready control), the rebuilding banner, the ready control sending,
/// and Health's pending (focusable line, Tab reaches it) and cli-only controls. Saved under the test output's frames/
/// as u1-*.png.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CatalogFramesTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CatalogFramesTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected(Func<ShellViewModel, bool> ready)
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !(shell.State == ShellState.Normal && ready(shell)); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static bool Visible(Visual within, string name) => within.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static void Frame(Window window, string name)
    {
        Settle(window);
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "u1-" + name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    // §4.1 Stalled: the views' own sentence, the ✕ glyph in Error, and Rebuild views (ready) under the top bar.
    [Fact]
    public Task The_stalled_banner_shows_the_sentence_and_a_ready_rebuild_views() => OnUi(async () =>
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        var (window, shell) = await Connected(s => s.Health.Banner.IsShown);

        var banner = Named<Control>(window, "ViewsBanner");
        Assert.True(banner.IsEffectivelyVisible);
        Assert.Equal("Views are stalled at 42: run `sindri views rebuild`", Named<TextBlock>(banner, "ViewsBannerText").Text);
        Assert.True(Visible(banner, "StalledGlyph"));
        var run = Named<Button>(banner, "RunButton");
        Assert.Equal((true, "Rebuild views"), (run.IsEffectivelyEnabled, AutomationProperties.GetName(run)));
        Assert.True(shell.Health.Banner.IsStalled);
        Frame(window, "banner-stalled");
    });

    [Fact]
    public Task The_rebuilding_banner_is_progress_without_a_button() => OnUi(async () =>
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "rebuilding");
        var (window, _) = await Connected(s => s.Health.Banner.IsShown);

        var banner = Named<Control>(window, "ViewsBanner");
        Assert.Equal("Rebuilding views (cursor 12 of 42)…", Named<TextBlock>(banner, "ViewsBannerText").Text);
        Assert.True(Visible(banner, "RebuildingGlyph"));
        Assert.False(Visible(banner, "RunButton"));
        Frame(window, "banner-rebuilding");
    });

    // §4.1 sending: disabled, its label kept, the spinner; the answer then shows the accepted outcome.
    [Fact]
    public Task Rebuild_views_shows_sending_then_its_outcome() => OnUi(async () =>
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        _server.RebuildGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (window, _) = await Connected(s => s.Health.Banner.IsShown);
        var banner = Named<Control>(window, "ViewsBanner");
        var run = Named<Button>(banner, "RunButton");

        run.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal((false, true, "Rebuild views"), (run.IsEffectivelyEnabled, Visible(banner, "RunSpinner"), Named<TextBlock>(banner, "RunLabel").Text));
        Frame(window, "control-sending");

        _server.RebuildGate.SetResult();
        for (var i = 0; i < 200 && !Visible(banner, "AcceptedLine"); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Equal("Rebuild views: done", Named<TextBlock>(banner, "OutcomeText").Text);
        Frame(window, "control-accepted");
    });

    // §7: pending rows are disabled with a focusable "arrives in <phase> · <cli>" line Tab reaches; cli-only rows are copy blocks.
    [Fact]
    public Task Health_shows_pending_lines_and_copy_blocks() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Health);
        Settle(window);

        var page = Named<Control>(window, "HealthPage");
        var lines = page.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "PendingLine" && b.IsEffectivelyVisible).ToList();
        Assert.Equal(shell.Health.Catalog.Pending.Sum(p => p.Controls.Count), lines.Count);
        Assert.All(lines, l => Assert.True(l.Focusable));
        Assert.Contains("arrives in P6 · sindri jobs cancel <jobId>", lines.Select(AutomationProperties.GetName), StringComparer.Ordinal);
        var pendingRun = page.GetVisualDescendants().OfType<Button>().First(b => b.Name == "RunButton" && b.IsEffectivelyVisible);
        Assert.False(pendingRun.IsEffectivelyEnabled);

        var blocks = page.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "CliText" && t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["sindri profile approve [<hash>]", "sindri context map import <file>", "sindri doctor", "sindri profile validate"], blocks, StringComparer.Ordinal);
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "CopyNoteText" && t.IsEffectivelyVisible && t.Text == Labels.TypedConfirmation);

        Assert.True(lines[0].Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, keySymbol: null);
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
        Settle(window);
        Assert.Same(lines[0], window.FocusManager?.GetFocusedElement());
        Frame(window, "health-controls");
    });
}
