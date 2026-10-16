using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>A filter is not a clear (review run 2, L1 / corr-0): a selected row only filtered out is dropped quietly.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouFilterSelectionTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filtering_out_the_selected_row_says_nothing_and_starts_no_quiet()
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        var feed = new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Decision()], "");
        list.Update(feed);
        list.Select("park:ITM-01JC8");

        list.Update(feed, e => e.Id.StartsWith("decision:", StringComparison.Ordinal));

        Assert.Equal("", list.Announcement);
        Assert.False(list.SelectionMovedOnItsOwn);
        Assert.NotEqual("park:ITM-01JC8", list.SelectedId, StringComparer.Ordinal);
        Assert.True(list.Press(NeedsYouKey.J));   // no KeyQuiet
    }

    [Fact]
    public async Task A_row_that_leaves_the_whole_feed_still_clears_with_its_sentence()
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        list.Update(new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Decision()], ""));
        list.Select("park:ITM-01JC8");

        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], ""), e => e.Id.StartsWith("decision:", StringComparison.Ordinal));

        Assert.Equal("ITM-01JC8 cleared", list.Announcement);
        Assert.True(list.SelectionMovedOnItsOwn);
    }
}
