using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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
/// The command palette and the `?` sheet in the window (UI spec §8, §11.2), against the in-memory API serving P1's
/// catalog: ⌘K / Ctrl+K and a click on the ⌘K field open it with focus in its field, ↓ and Enter run an entry, Esc
/// closes it and returns focus; `?` opens the shortcuts sheet (never while typing). Key frames palette.png,
/// palette-filtered.png and shortcuts.png go under the test output's frames/.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CommandPaletteTests : IDisposable
{
    private static readonly RawInputModifiers _command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandPaletteTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), _clipboard);
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && !(shell.State == ShellState.Normal && shell.Health.Catalog.Controls.Count > 0); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellState.Normal, shell.State);
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static bool Visible(Window window, string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name)?.IsEffectivelyVisible == true;

    private static Visual? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Visual;

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None, string? symbol = null)
    {
        window.KeyPress(key, modifiers, physical, symbol);
        Settle(window);
    }

    private static void Frame(Window window, string name)
    {
        Settle(window);
        var dir = Path.Combine(AppContext.BaseDirectory, "frames");
        _ = Directory.CreateDirectory(dir);
        window.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    // The entries drawn (each a button named by its label and status), in order.
    private static List<string> EntryNames(Window window) =>
        [.. Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => AutomationProperties.GetName(b) ?? "<unnamed>")];

    [Fact]
    public Task Command_K_opens_the_palette_with_focus_in_its_field_and_every_catalog_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Press(window, Key.K, PhysicalKey.K, _command, "k");

        Assert.True(shell.Palette.IsOpen);
        Assert.True(Visible(window, "CommandPalette"));
        Assert.Same(Named<TextBox>(window, "PaletteQuery"), Focused(window));
        var names = EntryNames(window);
        Assert.All(shell.Health.Catalog.Controls, c => Assert.Single(names, n => n.StartsWith(c.Label + ", ", StringComparison.Ordinal)));
        Assert.Contains("Run doctor, Copy: sindri doctor", names, StringComparer.Ordinal);
        Assert.Contains("Cancel job, arrives in P6", names, StringComparer.Ordinal);
        Assert.Equal(["Actions", "Go to", "Sindri"], Named<ItemsControl>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("palette-heading")).Select(t => t.Text ?? string.Empty), StringComparer.Ordinal);
        Frame(window, "palette");
    });

    [Fact]
    public Task Typing_filters_and_Enter_copies_a_cli_only_row_then_Esc_returns_focus_to_the_field() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var field = Named<Border>(window, "SearchField");
        Assert.True(field.Focus(NavigationMethod.Tab));

        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("approve profile");
        Settle(window);

        Assert.Equal(["Approve profile, Copy: sindri profile approve [<hash>]"], EntryNames(window), StringComparer.Ordinal);
        Press(window, Key.Enter, PhysicalKey.Enter);
        Assert.Equal("sindri profile approve [<hash>]", _clipboard.Text);
        Assert.Equal(["Approve profile, Copied"], EntryNames(window), StringComparer.Ordinal);
        Frame(window, "palette-filtered");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
        Assert.False(Visible(window, "CommandPalette"));
        Assert.Same(field, Focused(window));
    });

    [Fact]
    public Task Arrows_and_Enter_go_to_a_page_and_a_click_on_the_field_opens_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Named<Border>(window, "SearchField").RaiseEvent(new PointerPressedEventArgs(
            window, new Pointer(1, PointerType.Mouse, isPrimary: true), window, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Settle(window);
        Assert.True(shell.Palette.IsOpen);

        window.KeyTextInput("go");   // the Go to entries and nothing else ("go:" ids)
        Settle(window);
        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal("go:Queue", shell.Palette.Selected?.Id);
        Press(window, Key.Enter, PhysicalKey.Enter);

        Assert.Equal((PageId.Queue, false), (shell.CurrentPage, shell.Palette.IsOpen));
    });

    [Fact]
    public Task Question_mark_opens_the_shortcuts_sheet_and_Esc_closes_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var page = Named<Control>(window, "Page");
        Assert.True(page.Focus(NavigationMethod.Tab));

        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");

        Assert.True(shell.Shortcuts.IsOpen);
        Assert.True(Visible(window, "ShortcutsSheet"));
        Assert.Contains(Named<ItemsControl>(window, "ShortcutRows").GetVisualDescendants().OfType<Control>(),
            c => AutomationProperties.GetName(c) == "⌘K / Ctrl+K: Command palette");
        Frame(window, "shortcuts");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(Visible(window, "ShortcutsSheet"));
        Assert.Same(page, Focused(window));
    });

    // §8: single keys never fire while typing in a text field.
    [Fact]
    public Task A_question_mark_typed_in_the_palette_stays_in_its_field() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        Press(window, Key.K, PhysicalKey.K, _command, "k");

        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");
        window.KeyTextInput("?");
        Settle(window);

        Assert.False(shell.Shortcuts.IsOpen);
        Assert.Equal("?", shell.Palette.Query);
    });

    private static async Task Until(Window window, Func<bool> condition, string what)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.True(condition(), what);
    }

    private static Button EntryButton(Window window, string name) =>
        Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b)?.StartsWith(name, StringComparison.Ordinal) == true);

    // F1: the palette opens, and each refilter shows, at the top with its first heading in view and the first entry
    // that runs selected; only ↑/↓ scroll the list to follow the selection.
    [Fact]
    public Task The_palette_opens_and_refilters_at_the_top_and_only_the_arrows_scroll() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Press(window, Key.K, PhysicalKey.K, _command, "k");
        var scroll = Named<ScrollViewer>(window, "PaletteScroll");

        Assert.Equal(("ApproveProfile", 0d), (shell.Palette.Selected?.Id, scroll.Offset.Y));
        var heading = Named<ItemsControl>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Actions");
        Assert.True(heading.TranslatePoint(default, scroll) is { Y: >= 0 } at && at.Y < scroll.Viewport.Height, "the Actions heading is in view");
        Frame(window, "palette-top");

        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Assert.True(scroll.Offset.Y > 0, "the arrows bring the selection into view");

        window.KeyTextInput("o");
        Settle(window);
        Assert.Equal(0d, scroll.Offset.Y);
        Assert.Same(shell.Palette.Entries.First(e => e.IsEnabled), shell.Palette.Selected);
    });

    // F2 (provisional default): Enter on a ready entry keeps the palette open; the entry shows sending (disabled, the
    // label kept, a spinner), then done; Esc still closes it.
    [Fact]
    public Task A_ready_entry_shows_sending_then_done_in_the_open_palette() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _server.RebuildGate = new TaskCompletionSource();
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("rebuild");
        Settle(window);

        Press(window, Key.Enter, PhysicalKey.Enter);

        Assert.True(shell.Palette.IsOpen);
        var entry = EntryButton(window, "Rebuild views");
        Assert.Equal(("Rebuild views, Sending…", false), (AutomationProperties.GetName(entry), entry.IsEnabled));
        Assert.True(entry.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("entry-spinner")).IsEffectivelyVisible);
        Frame(window, "palette-sending");

        _server.RebuildGate.SetResult();
        await Until(window, () => AutomationProperties.GetName(EntryButton(window, "Rebuild views")) == "Rebuild views, Rebuild views: done", "done");
        Assert.True(shell.Palette.IsOpen);
        Frame(window, "palette-done");

        Press(window, Key.Escape, PhysicalKey.Escape);
        Assert.False(shell.Palette.IsOpen);
    });

    // F2: a refusal shows Sindri's code as the entry's status and the detail as served under it; the palette stays open.
    [Fact]
    public Task A_refused_entry_shows_sindris_code_and_detail_in_the_open_palette() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005",
            "POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it");
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        window.KeyTextInput("rebuild");
        Settle(window);

        Press(window, Key.Enter, PhysicalKey.Enter);
        await Until(window, () => shell.Palette.Selected?.IsFailed == true, "refused");

        Assert.True(shell.Palette.IsOpen);
        var detail = Named<Control>(window, "PaletteSections").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("entry-error-detail"));
        Assert.True(detail.IsEffectivelyVisible);
        Assert.Equal("POST /sindri/views/rebuild failed with NotSupportedException; the control plane logged it", detail.Text);
        Assert.StartsWith("Rebuild views, SND-API-005: ", AutomationProperties.GetName(EntryButton(window, "Rebuild views")), StringComparison.Ordinal);
        Frame(window, "palette-refused");
    });
}
