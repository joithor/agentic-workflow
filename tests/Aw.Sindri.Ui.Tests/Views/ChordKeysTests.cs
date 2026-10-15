using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The G-chords as the window routes them (screens spec §3, rev 7 §8): gated like the other navigation, never typed into a text field.</summary>
[Lane(TestLane.Integration)]
public sealed class ChordKeysTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ChordKeysTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Show(ShellState state)
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 200 && shell.State != state; i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(state, shell.State);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task G_then_W_navigates_to_Workflows() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
    });

    [Fact]
    public Task Ctrl_G_does_not_start_a_chord() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G, RawInputModifiers.Control);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    [Fact]
    public Task Typing_in_a_text_field_never_chords() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        var field = new TextBox();
        window.SettingsSlot.Content = field;
        shell.GoTo(PageId.Settings);
        Dispatcher.UIThread.RunJobs();
        _ = field.Focus();

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Settings, shell.CurrentPage);
    });

    [Fact]
    public Task The_palette_open_takes_the_keys_and_chords_nothing() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);
        shell.Palette.Open();
        Dispatcher.UIThread.RunJobs();

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });

    [Fact]
    public Task Not_running_leaves_the_page_and_the_last_page_alone() => OnUi(async () =>
    {
        var (window, shell) = await Show(ShellState.NotRunning);
        shell.GoTo(PageId.Context);

        Press(window, Key.G);
        Press(window, Key.W);

        Assert.False(shell.IsChordPending);
        Assert.Equal(PageId.Context, shell.CurrentPage);
        Assert.Equal("Context", (await new Preferences(_preferences).Load(Token)).LastPage);
    });

    [Theory]
    [InlineData(Key.Down)]
    [InlineData(Key.Enter)]
    [InlineData(Key.D1)]
    [InlineData(Key.Escape)]
    public Task Any_other_unmodified_key_cancels_a_waiting_chord(Key other) => OnUi(async () =>
    {
        _records.Write(A.Record());
        var (window, shell) = await Show(ShellState.Normal);

        Press(window, Key.G);
        Assert.True(shell.IsChordPending);
        Press(window, other);

        Assert.False(shell.IsChordPending);
        Press(window, Key.W);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    });
}
