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
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The shell window under the headless platform, bound to a shell following the in-memory API: one key frame per pre-API
/// state (UI spec §4.1, §18.2), saved under the test output's frames/ as shell-*.png. Plain xUnit v3 facts that dispatch
/// onto the headless session's UI thread: Avalonia.Headless.XUnit 12.1.3 is built against xUnit v3 3.2 and fails
/// discovery under the repo's 4.0 (docs/csharp/sindri-ui.md "Tests").
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ShellWindowTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ShellWindowTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(Func<ShellViewModel, bool> ready, bool run = true)
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        window.Show();
        var following = run ? shell.Run(Token) : Task.CompletedTask;
        await Until(() => ready(shell) || following.IsFaulted);
        Assert.False(following.IsFaulted, following.Exception?.ToString());
        Assert.True(ready(shell), "the shell never reached the expected state");
        Settle(window);
        return (window, shell);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static string Text(Window window, string name) => window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == name)?.Text ?? "<missing>";

    private static bool Visible(Window window, string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static void Frame(Window window, string name)
    {
        Settle(window);
        var frame = window.CaptureRenderedFrame();
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        frame?.Save(Path.Combine(dir, "shell-" + name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    // Every visible control in the window carries an accessible name (§11.2).
    private static void EveryControlIsNamed(Window window)
    {
        var unnamed = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible
                && (c is Button or ListBoxItem or TextBlock { Text.Length: > 1 })
                && string.IsNullOrEmpty(AutomationProperties.GetName(c))
                && (c is not TextBlock || (c.FindAncestorOfType<Button>() is null && c.FindAncestorOfType<ListBoxItem>() is null)))
            .Select(c => c.GetType().Name + ":" + (c as TextBlock)?.Text)
            .ToList();
        Assert.True(unnamed.Count == 0, "unnamed: " + string.Join(", ", unnamed));
    }

    [Fact]
    public Task Connecting_shows_the_chrome_and_skeleton_rows() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);

        Assert.True(Visible(window, "TopBar"));
        Assert.True(Visible(window, "ConnectingSkeleton"));
        Assert.False(Visible(window, "Pages"));
        Assert.Equal(Labels.Connecting, Text(window, "HealthPillText"));
        Frame(window, "connecting");
    });

    [Fact]
    public Task Not_running_shows_the_reason_and_the_sindri_fix() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        Assert.True(Visible(window, "NotRunningPanel"));
        Assert.False(Visible(window, "Pages"));
        Assert.Equal(Labels.NoRecord(_records.Location), Text(window, "ReasonText"));
        Assert.Equal("sindri", Text(window, "FixText"));
        Assert.Equal(Labels.NotRunning, Text(window, "HealthPillText"));
        EveryControlIsNamed(window);
        Frame(window, "not-running");
    });

    [Fact]
    public Task A_version_mismatch_takes_the_whole_window() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, _) = await Show(s => s.State == ShellState.VersionMismatch);

        Assert.True(Visible(window, "VersionMismatchPanel"));
        Assert.False(Visible(window, "TopBar"));
        Assert.Equal(Labels.Versions(2, 1), Text(window, "VersionsText"));
        Assert.Equal(Labels.VersionMismatchFix, Text(window, "VersionFixText"));
        Frame(window, "version-mismatch");
    });

    [Fact]
    public Task Normal_shows_Home_with_its_four_panel_frames() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        Assert.True(Visible(window, "HomePage"));
        foreach (var panel in (string[])["NeedsYouPanel", "RunningPanel", "QueuedPanel", "HealthStrip"])
        {
            Assert.True(Visible(window, panel), panel);
        }

        Assert.Equal("Running · updated 0s ago", Text(window, "HealthPillText"));
        Assert.False(Visible(window, "AttentionBand"));
        Assert.Equal(1280, window.Width);
        Assert.Equal((900d, 600d), (window.MinWidth, window.MinHeight));
        EveryControlIsNamed(window);
        Frame(window, "normal");
    });

    [Fact]
    public Task Unreadable_shows_the_banner_and_keeps_the_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;
        var (window, _) = await Show(s => s.State == ShellState.Unreadable);

        Assert.True(Visible(window, "UnreadableBanner"));
        Assert.True(Visible(window, "Pages"));
        Assert.Contains("SND-UI-003", Text(window, "UnreadableText"), StringComparison.Ordinal);
        Assert.Equal("sindri doctor", Text(window, "DoctorFixText"));
        Frame(window, "unreadable");
    });

    [Fact]
    public Task Stale_says_so_in_the_pill() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Equal(Labels.Running + " · " + Labels.Stale, Text(window, "HealthPillText"));
        Frame(window, "stale");
    });

    [Fact]
    public Task The_health_pill_opens_Health_with_S1bs_status_card() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile());
        _server.Push(A.Event(3));
        var (window, shell) = await Show(s => s.State == ShellState.Normal && s.Health.LastEvent.EndsWith("#3", StringComparison.Ordinal));

        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HealthPill").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal(PageId.Health, shell.CurrentPage);
        Assert.True(Visible(window, "HealthPage"));
        Assert.Equal("pid 4242 · epoch 7", Text(window, "LeaseText"));
        Assert.Equal("changed since approval", Text(window, "ProfileStateText"));
        Assert.Equal("sindri profile approve", Text(window, "ProfileFixText"));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "last run 12:00:00Z");
        EveryControlIsNamed(window);
        Frame(window, "health");
    });

    [Fact]
    public Task A_deep_link_opens_the_sheet_and_Esc_leaves_the_edge_tab() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));

        shell.Navigate(link);
        Settle(window);
        Assert.True(Visible(window, "Sheet"));
        Assert.Equal("ITM-01JC8", Text(window, "SheetTitle"));
        Assert.Equal(UiPreferences.DefaultSheetWidth, window.Get<Border>("Sheet").Bounds.Width);
        Frame(window, "sheet");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);
        Assert.False(Visible(window, "Sheet"));
        Assert.True(Visible(window, "EdgeTab"));
        Frame(window, "edge-tab");
    });

    [Fact]
    public Task Below_1100_px_the_sheet_covers_the_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Save(UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 1000, 700) }, Token);
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("decision/DEC-7", out var link));

        shell.Navigate(link);
        Settle(window);

        Assert.Equal(1000, window.Width);
        Assert.Contains("covers", window.Get<Border>("Sheet").Classes, StringComparer.Ordinal);
        Assert.True(Visible(window, "SheetBack"));
        Assert.Equal(window.Get<Border>("Sheet").Bounds.Width, window.ClientSize.Width, precision: 0);
        Frame(window, "sheet-covers");
    });

    [Fact]
    public Task F6_cycles_top_bar_page_and_sheet() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-1", out var link));
        shell.Navigate(link);
        Settle(window);

        string Region()
        {
            var focused = window.FocusManager?.GetFocusedElement() as Visual;
            string[] names = ["TopBar", "Page", "Sheet"];
            return names.FirstOrDefault(n => focused is not null && window.Get<Control>(n) is var r && (r == focused || r.IsVisualAncestorOf(focused))) ?? "none";
        }

        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
            Settle(window);
            seen.Add(Region());
        }

        Assert.Equal(["TopBar", "Page", "Sheet", "TopBar"], seen);
    });

    [Fact]
    public Task Command_comma_opens_Settings() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPress(Key.OemComma, command, PhysicalKey.Comma, keySymbol: ",");
        Settle(window);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
        Assert.True(Visible(window, "SettingsSlot"));
    });

    [Fact]
    public Task Closing_remembers_the_window_and_reopening_restores_it() => OnUi(async () =>
    {
        await new Preferences(_preferences).Save(UiPreferences.Defaults with { Window = new WindowBounds(40, 50, 1100, 720) }, Token);
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);
        Assert.Equal((1100d, 720d), (window.Width, window.Height));

        window.Close();

        var stored = (await new Preferences(_preferences).Load(Token)).Window;
        Assert.NotNull(stored);
        Assert.Equal((1100d, 720d), (stored.Width, stored.Height));
    });
}
