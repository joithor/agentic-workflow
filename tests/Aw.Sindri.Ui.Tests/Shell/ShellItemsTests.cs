using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's U2 parts (UI spec §3.3, §8, §9.2, §18 U2): the queue re-read on every event without a click, the item
/// sheet from a deep link, the Queue tab's count, and the single keys (G chord, J/K, Enter, C).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellItemsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    // P3 serves no waitingOnYou rows; the P4 extra adds the needs-decision row (a pending Decide) these behaviours need.
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.SpecShaped("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Navigation[1].Count.Length > 0 && shell.Queue.Groups.Count > 0);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition(), "the condition never held");
    }

    // §18 U2's signal: `item add` at the CLI appears without a click: the event makes the app re-read the queue (§9.2).
    [Fact]
    public async Task A_new_item_appears_on_the_next_event_without_a_click()
    {
        var shell = await Connected();
        Assert.Equal(5, shell.Queue.Total);
        Assert.Equal("5", shell.Navigation[1].Count);

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        _server.Push(A.Event(58, "item.created"));

        await Until(() => shell.Queue.Total == 12);
        Assert.Equal("12", shell.Navigation[1].Count);
    }

    // §3.3 deep link item/<ItemId>: the sheet shows the item; every event re-reads it too.
    [Fact]
    public async Task An_item_link_opens_its_sheet_and_events_re_read_it()
    {
        var shell = await Connected();

        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        await Until(() => shell.Item.HasItem);
        Assert.Equal((true, true, "Try the queue"), (shell.IsSheetShown, shell.IsItemSheet, shell.Item.Title));

        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        _server.Push(A.Event(59, "item.state-changed"));
        await Until(() => shell.Item.StateWord == "Done");
    }

    // §8: G then Q / R / W within 1 s; the chord shows "G …" while it waits and lapses after 1 s.
    [Fact]
    public async Task The_g_chord_goes_to_the_queue_and_its_filters()
    {
        var shell = await Connected();

        Assert.True(shell.Press("G"));
        Assert.Equal("G …", shell.ChordHint);
        Assert.True(shell.Press("R"));
        Assert.Equal((PageId.Queue, QueueFilter.Running, string.Empty), (shell.CurrentPage, shell.Queue.Showing, shell.ChordHint));

        _ = shell.Press("G");
        _clock.Advance(ShellViewModel.ChordWait + TimeSpan.FromMilliseconds(1));
        Assert.False(shell.Press("H"));
        Assert.Equal(PageId.Queue, shell.CurrentPage);

        _ = shell.Press("G");
        _ = shell.Press("Q");
        Assert.Equal(QueueFilter.All, shell.Queue.Showing);
    }

    // §8: J moves to the next row of the page it is pressed on; Enter opens its sheet; C opens the create form.
    [Fact]
    public async Task J_and_enter_open_the_selected_rows_sheet_and_c_opens_create()
    {
        var shell = await Connected();
        shell.GoTo(PageId.Queue);

        Assert.True(shell.Press("J"));
        Assert.True(shell.Press("Enter"));
        Assert.Equal((A.DecisionItem, true), (shell.SheetSubject, shell.IsItemSheet));

        Assert.True(shell.Press("C"));
        Assert.True(shell.Create.IsOpen);
        Assert.False(shell.Press("X"));
    }

    // L2 (§3.3): on Home J walks Home's own rows (Queued here; the waiting row is not drawn on Home), and Enter opens that row.
    [Fact]
    public async Task J_on_home_walks_the_rows_home_draws()
    {
        var shell = await Connected();

        Assert.True(shell.Press("J"));
        Assert.Equal(shell.Queue.QueuedRows[0].Id, shell.Queue.SelectedId);
        Assert.True(shell.Press("J"));
        Assert.Equal(shell.Queue.QueuedRows[1].Id, shell.Queue.SelectedId);
        Assert.True(shell.Press("Enter"));
        Assert.Equal(shell.Queue.QueuedRows[1].Id, shell.SheetSubject);
    }

    // L2 (§8, §11.2): keys are scoped to the focused region: in the sheet, J / K / Enter / Y are not the queue's.
    [Fact]
    public async Task The_row_keys_belong_to_the_sheet_while_focus_is_in_it()
    {
        var shell = await Connected();
        shell.GoTo(PageId.Queue);
        _ = shell.Press("J");
        var selected = shell.Queue.SelectedId;

        Assert.False(shell.Press("J", inSheet: true));
        Assert.False(shell.Press("Enter", inSheet: true));
        Assert.False(shell.Press("Y", inSheet: true));
        Assert.Equal((selected, false), (shell.Queue.SelectedId, shell.IsSheetOpen));
        Assert.True(shell.Press("C", inSheet: true));
    }

    // §8 Y: copies the selected row's CLI ("Copied" through the control's lifecycle); / opens the Queue's filter.
    [Fact]
    public async Task Y_copies_the_selected_rows_cli_and_slash_opens_the_filter()
    {
        var clipboard = new FakeClipboard();
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), clipboard);
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0);

        Assert.False(shell.Press("/"));
        shell.GoTo(PageId.Queue);
        _ = shell.Press("J");
        Assert.True(shell.Press("Y"));
        await Until(() => clipboard.Text is not null);
        Assert.Equal("sindri decide <decisionId> <choice> [--note <note>]", clipboard.Text);

        Assert.True(shell.Press("/"));
        Assert.True(shell.Queue.IsFiltering);
    }

    // §8 key guard: while it holds, single keys do nothing.
    [Fact]
    public async Task Single_keys_are_ignored_while_the_key_guard_holds()
    {
        var shell = await Connected();
        shell.Queue.Select(A.NewItem);
        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(A.NewItem, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        _server.Push(A.Event(58, "item.state-changed"));
        await Until(() => shell.Queue.IsGuarded);

        Assert.True(shell.Press("C"));
        Assert.False(shell.Create.IsOpen);

        _clock.Advance(QueueViewModel.KeyGuard);
        Assert.True(shell.Press("C"));
        Assert.True(shell.Create.IsOpen);
    }
}
