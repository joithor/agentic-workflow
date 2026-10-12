using System.Globalization;
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
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
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
public sealed class ShellWindowTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    // Each test stops its shell and closes its windows, then settles, before it returns: a layout pass or a ticker left
    // queued would run after the headless session has moved on (another test's theme, or the session's reset).
    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ShellWindowTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(Func<ShellViewModel, bool> ready, bool run = true)
    {
        var shell = new ShellViewModel(_records, _server, _clock, _preferences);
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        var following = run ? shell.Run(_stop.Token) : Task.CompletedTask;
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

    // A control by name anywhere in the window, across the views' own name scopes (TopBar, HomePage).
    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

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

    // §14.2: every text is a step of the type ramp, so the Text size preference reaches all of it. At scale 1.3 each
    // visible TextBlock's size is a ramp size × 1.3 (a literal size, or one Fluent's own default sets, stays put). The
    // scale is applied, checked and reset without yielding the UI thread, so no other test sees it.
    private static readonly double[] _ramp = [20, 18, 13, 12, 11];

    private static void EveryTextIsOnTheRampAt130(Window window)
    {
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, 1.3);
        try
        {
            Settle(window);
            var off = window.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && !_ramp.Any(r => Math.Abs(t.FontSize - (r * 1.3)) < 0.01))
                .Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.Name ?? t.Text}={t.FontSize}"))
                .ToList();
            Assert.True(off.Count == 0, "off the ramp at 130%: " + string.Join(", ", off));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
            Settle(window);
        }
    }

    [Fact]
    public Task Every_text_on_Home_Health_and_the_sheet_follows_the_text_scale() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        EveryTextIsOnTheRampAt130(window);

        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        shell.GoTo(PageId.Health);
        Settle(window);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_while_not_running_follows_the_text_scale() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_while_connecting_follows_the_text_scale() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.Connecting, run: false);
        EveryTextIsOnTheRampAt130(window);
    });

    [Fact]
    public Task Every_text_on_a_version_mismatch_follows_the_text_scale() => OnUi(async () =>
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };
        var (window, _) = await Show(s => s.State == ShellState.VersionMismatch);
        EveryTextIsOnTheRampAt130(window);
    });

    // I6: the sheet's heading is DetailTitle (18/500) in the mono family; .mono's later size doesn't win.
    [Fact]
    public Task The_sheet_heading_is_a_mono_detail_title() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));
        shell.Navigate(link);
        Settle(window);

        var title = window.Get<TextBlock>("SheetTitle");
        Assert.Equal(18, title.FontSize);
        Assert.Equal(Avalonia.Media.FontWeight.Medium, title.FontWeight);
        Assert.Contains("JetBrains Mono", title.FontFamily.ToString(), StringComparison.Ordinal);
    });

    private static Avalonia.Media.Color Ink(TextBlock t) => ((Avalonia.Media.ISolidColorBrush)t.Foreground!).Color;

    private static Avalonia.Media.Color Colour(string key) =>
        Application.Current!.TryGetResource(key, theme: null, out var v) && v is Avalonia.Media.Color c ? c : default;

    // I1 (home.html .seg): one group; unselected labels muted, the selected one in PrimaryHover with the Primary dot;
    // Later ▾ a flat segment of the same group; the whole group disabled while not running.
    [Fact]
    public Task The_navigation_is_one_segmented_group_with_the_selected_dot() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);
        var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label") && t.Text is { Length: > 1 }).ToList();

        Assert.Equal(Colour("Color.PrimaryHover"), Ink(labels.Single(t => t.Text == Labels.Home)));
        Assert.All(labels.Where(t => t.Text != Labels.Home), t => Assert.Equal(Colour("Color.InkSubtle"), Ink(t)));
        _ = Assert.Single(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>(), e => e.Classes.Contains("nav-dot") && e.IsEffectivelyVisible);
        var group = Named<StackPanel>(window, "NavigationGroup");
        Assert.True(group.IsVisualAncestorOf(Named<Button>(window, "LaterButton")));
        Assert.True(group.IsVisualAncestorOf(Named<ListBox>(window, "Navigation")));
        Frame(window, "nav");
    });

    [Fact]
    public Task Not_running_dims_and_disables_the_navigation() => OnUi(async () =>
    {
        var (window, _) = await Show(s => s.State == ShellState.NotRunning);

        Assert.False(Named<StackPanel>(window, "NavigationGroup").IsEffectivelyEnabled);
        Assert.All(window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-label")),
            t => Assert.Equal(Colour("Color.InkTertiary"), Ink(t)));
    });

    // I3 (home.html .phead): each Home panel has a header row (the eyebrow over a hairline) and a body; "not served yet"
    // is a quiet InkSubtle caption. N2/N6: panel insets and the page gutter are on the spacing scale (16, 24).
    [Fact]
    public Task Home_panels_have_a_header_row_and_a_quiet_body() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        foreach (var name in (string[])["NeedsYouPanel", "RunningPanel", "QueuedPanel"])
        {
            var panel = Named<Border>(window, name);
            var head = panel.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("phead"));
            Assert.Equal(new Thickness(0, 0, 0, 1), head.BorderThickness);
            Assert.Contains(head.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("eyebrow"));
            var quiet = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == Labels.NotServedYet);
            Assert.Contains("caption", quiet.Classes, StringComparer.Ordinal);
            Assert.Equal(Colour("Color.InkSubtle"), Ink(quiet));
        }

        Assert.Equal(new Thickness(24), Named<Panel>(window, "PageContent").Margin);
        Frame(window, "home-panels");
    });

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
        Assert.Equal(Labels.GlyphDown, Text(window, "PillGlyph"));
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

        Assert.Equal((Labels.GlyphOk, Labels.Running, "updated 0s ago"), (Text(window, "PillGlyph"), Text(window, "HealthPillText"), Text(window, "PillAgeText")));
        Assert.Equal("Running · updated 0s ago", AutomationProperties.GetName(Named<Button>(window, "HealthPill")));
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
        _server.FailPing = true;

        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);

        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Equal((Labels.GlyphStale, Labels.Stale), (Text(window, "PillGlyph"), Text(window, "HealthPillText")));
        Assert.Equal(Labels.UpdatedAgo(ShellViewModel.StaleAfter), Text(window, "PillAgeText"));
        Assert.True(Visible(window, "RefreshButton"));
        Assert.True(Visible(window, "PillDot"));
        EveryControlIsNamed(window);
        Frame(window, "stale");

        var before = _server.StatusCalls;
        Named<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => shell.State == ShellState.Normal);
        Assert.Equal(before + 1, _server.StatusCalls);
        Assert.False(Visible(window, "RefreshButton"));
        Assert.Equal((Labels.GlyphOk, Labels.Running), (Text(window, "PillGlyph"), Text(window, "HealthPillText")));
    });

    // B2 on the Health page: stale reads ⌛ stale at the process section's head.
    [Fact]
    public Task Stale_says_so_on_the_Health_page() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        shell.GoTo(PageId.Health);
        _server.FailPing = true;
        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);
        Settle(window);

        Assert.Equal((Labels.GlyphStale, Labels.Stale), (Text(window, "ProcessGlyph"), Text(window, "ProcessWord")));
        Frame(window, "health-stale");
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

        // I5 (services-health.html HEALTH): label/value rows at Body size under section heads that carry the state's
        // glyph and word; no display-size values and no badge repeating the pill.
        List<string?> Rows(string name) => [.. Named<ItemsControl>(window, name).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)];
        Assert.Equal(["pid · epoch", "4242 · 7", "Host lease", "held · expires " + A.Noon.AddSeconds(60).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture), "API", "127.0.0.1:7410"], Rows("ProcessRows"));
        Assert.Equal((Labels.GlyphOk, Labels.Running), (Text(window, "ProcessGlyph"), Text(window, "ProcessWord")));
        Assert.Equal("◇ changed since approval", Text(window, "ProfileHeadText"));
        Assert.Equal(["approved", "ab12cd", "live", "ff00ee", "file", "/home/profile.yaml"], Rows("ProfileRows"));
        Assert.Equal("sindri profile approve", Text(window, "ProfileFixText"));
        Assert.False(Visible(window, "HeadlineText"));
        var page = Named<Control>(window, "HealthPage");
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.FontSize > 13 && !t.Classes.Contains("page-title"));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "last run " + A.Noon.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        EveryControlIsNamed(window);
        Frame(window, "health");
    });

    [Fact]
    public Task Homes_health_strip_shows_the_health_line_and_opens_Health() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        // I4 (home.html): labelled cells, the G . hint and a trailing ›; still one button named by the health line.
        var cells = Named<ItemsControl>(window, "HealthCells");
        var texts = cells.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["Process", "Running · pid 4242", "Epoch · lease", "7 · held", "Services", "1 service", "Profile", "✓ approved"], texts);
        Assert.True(Visible(window, "HealthChevron"));
        Assert.True(Visible(window, "HealthKeys"));
        var strip = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "HealthStrip");
        Assert.Equal(shell.Health.HealthLine, AutomationProperties.GetName(strip));
        EveryControlIsNamed(window);
        Frame(window, "home-health-strip");

        strip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.Equal(PageId.Health, shell.CurrentPage);

        shell.GoTo(PageId.Home);
        Settle(window);
        Assert.True(strip.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        Settle(window);
        Assert.Equal(PageId.Health, shell.CurrentPage);
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

    // §3.2: window minimum 900 × 600, and the top bar still fits there: the ⌘K field gives way, the pill stays whole
    // (with room for its focus ring), and nothing in the bar overlaps.
    [Fact]
    public Task At_the_900_px_minimum_the_top_bar_fits_with_the_pill_whole() => OnUi(async () =>
    {
        _records.Write(A.Record());
        await new Preferences(_preferences).Save(UiPreferences.Defaults with { Window = new WindowBounds(0, 0, 900, 600) }, Token);
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        Assert.Equal(900, window.ClientSize.Width);
        var bar = _topBarControls.Select(n => Box(window, n)).ToList();
        var pill = bar[^1];
        Assert.True(pill.Right <= window.ClientSize.Width - 4, string.Create(CultureInfo.InvariantCulture, $"the pill ends at {pill.Right}, past the window's {window.ClientSize.Width} less its ring"));
        for (var i = 1; i < bar.Count; i++)
        {
            Assert.True(bar[i - 1].Right <= bar[i].Left, $"{bar[i - 1]} overlaps {bar[i]}");
        }

        Frame(window, "min-width-top-bar");
    });

    // With room the ⌘K field keeps its full 300 px and the pill sits at the right edge (inside the top bar's 16 px padding).
    [Fact]
    public Task With_room_the_search_field_keeps_its_full_width() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, _) = await Show(s => s.State == ShellState.Normal);

        Assert.Equal(300, Box(window, "SearchField").Width, precision: 0);
        Assert.Equal(window.ClientSize.Width - 16, Box(window, "HealthPill").Right, precision: 0);
    });

    // §11.2: the page region's accessible name follows the page (it read "Home" on every page).
    [Fact]
    public Task The_page_region_is_named_after_the_page_shown() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);

        shell.GoTo(PageId.Queue);
        Settle(window);
        Assert.Equal(Labels.Queue, AutomationProperties.GetName(window.Get<Border>("Page")));

        shell.GoTo(PageId.Settings);
        Settle(window);
        Assert.Equal(Labels.Settings, AutomationProperties.GetName(window.Get<Border>("Page")));
    });

    // Settings sits at the same left inset as every other page, so switching pages doesn't shift the title sideways.
    [Fact]
    public Task Settings_title_sits_where_every_page_title_does() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(s => s.State == ShellState.Normal);
        var settings = new SettingsViewModel(new Preferences(_preferences), new FakeMotionPreference(), _ => { });
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        Point Title() => window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.IsEffectivelyVisible && t.Classes.Contains("page-title")) is var title
            ? title.TranslatePoint(new Point(0, 0), window) ?? default : default;

        var home = Title();
        shell.GoTo(PageId.Health);
        Settle(window);
        var health = Title();
        shell.GoTo(PageId.Settings);
        Settle(window);
        var onSettings = Title();

        Assert.Equal(home.X, health.X);
        Assert.Equal(home.X, onSettings.X);
        Frame(window, "settings");
    });

    private static readonly string[] _topBarControls = ["Navigation", "LaterButton", "SearchField", "HealthPill"];

    private static Rect Box(Window window, string name)
    {
        var control = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);
        return new Rect(control.TranslatePoint(new Point(0, 0), window) ?? default, control.Bounds.Size);
    }

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
