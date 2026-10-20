using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox page's view model (needs-you spec §2, slice 1): the grouped list, the selection that survives a re-read, and the request card's plain words.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouPageViewModelTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(NeedsYouViewModel List, NeedsYouPageViewModel Page)> Page(params NeedsYouEntry[] rows)
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        list.Update(new NeedsYouFeed(rows, rows.Length == 0 ? "Nothing needs you. 1 running, 2 queued." : ""));
        return (list, new NeedsYouPageViewModel(list, _clock, _clipboard));
    }

    [Fact]
    public async Task Groups_carry_their_heading_and_count_and_hold_the_row_view_models_of_the_list()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("park:WFI-1"), NeedsYouRows.Park("desk:WFI-2"));

        Assert.Equal([("Your approvals", "1"), ("Paused runs", "1"), ("Agent decisions", "1"), ("Something failed", "1")], page.Groups.Select(g => (g.Heading, g.CountText)));
        Assert.Same(list.Seen.Rows.Single(r => r.Id == "desk:WFI-2"), page.Groups[0].Rows.Single());
    }

    [Fact]
    public async Task The_header_counts_the_rows_and_the_paused_runs()
    {
        var (_, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Park("park:WFI-1"), NeedsYouRows.Park("takeover:WFI-2"));

        Assert.Equal("3 waiting, 2 runs", page.CountText);
    }

    [Fact]
    public async Task Opening_selects_the_first_row_of_the_first_group_unless_one_is_selected()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision());

        page.SelectFirst();
        Assert.Equal("decision:DEC-1", list.SelectedId);

        list.Select("failure:service:index");
        page.SelectFirst();
        Assert.Equal("failure:service:index", list.SelectedId);
    }

    [Fact]
    public async Task J_and_K_walk_the_groups_in_the_pages_order_and_stop_at_the_ends()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("desk:WFI-2"));
        page.SelectFirst();

        var seen = new List<string?> { list.SelectedId };
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(-1);
        seen.Add(list.SelectedId);

        Assert.Equal(["desk:WFI-2", "decision:DEC-1", "failure:service:index", "failure:service:index", "decision:DEC-1"], seen);
    }

    [Fact]
    public async Task The_card_follows_the_selected_row_in_plain_words()
    {
        var (list, page) = await Page(NeedsYouRows.Park("desk:WFI-2", minutes: 351), NeedsYouRows.Decision());

        list.Select("desk:WFI-2");

        Assert.Equal("desk:WFI-2", page.Selected?.Id);
        Assert.Equal(("Your approval", "Waiting for you", "waiting 5h 51m"), (page.KindWord, page.FooterLeft, page.FooterRight));

        list.Select("decision:DEC-1");
        Assert.Equal(("Agent decision", "waiting 26m"), (page.KindWord, page.FooterRight));
    }

    [Fact]
    public async Task The_card_names_every_kind_by_its_own_word()
    {
        var (list, page) = await Page(NeedsYouRows.Park("park:WFI-1"), NeedsYouRows.Park("takeover:WFI-2"), NeedsYouRows.Profile(), NeedsYouRows.Failure());

        var words = new List<string>();
        foreach (var id in new[] { "park:WFI-1", "takeover:WFI-2", "profile:drift", "failure:service:index" })
        {
            list.Select(id);
            words.Add(page.KindWord);
        }

        Assert.Equal(["Paused run", "Paused run", "Settings change", "Something failed"], words);
    }

    [Fact]
    public async Task A_selected_row_stays_selected_across_a_re_read_by_its_id()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("failure:service:index");

        list.Update(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("desk:WFI-2")], ""));

        Assert.Equal("failure:service:index", page.Selected?.Id);
    }

    [Fact]
    public async Task A_row_that_clears_moves_the_selection_to_the_next_row()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("decision:DEC-1");

        list.Update(new NeedsYouFeed([NeedsYouRows.Failure()], ""));

        Assert.Equal("failure:service:index", page.Selected?.Id);
    }

    [Fact]
    public async Task With_no_rows_there_is_no_card_and_no_group()
    {
        var (_, page) = await Page();

        Assert.Empty(page.Groups);
        Assert.Null(page.Selected);
        Assert.Equal("0 waiting, 0 runs", page.CountText);
    }
}
