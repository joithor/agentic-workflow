using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>When the Agents page reads (Q9): on events and every 5 s, only while it is shown and the window is up.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentsPagePollTests
{
    private readonly AgentsPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Polls_every_5s_only_while_visible()
    {
        var page = await _f.Shown(Token);
        Assert.Equal(1, page.Drawn);

        _f.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(2, page.Drawn);
        _f.Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(2, page.Drawn);
        _f.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, page.Drawn);

        page.SetShown(shown: false);
        _f.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(3, page.Drawn);
    }

    [Fact]
    public async Task Stops_polling_when_minimised_and_reads_at_once_when_restored()
    {
        var page = await _f.Shown(Token);

        page.SetMinimised(minimised: true);
        _f.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(1, page.Drawn);

        page.SetMinimised(minimised: false);
        Assert.Equal(2, page.Drawn);
        _f.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(3, page.Drawn);
    }

    [Fact]
    public async Task Reads_on_an_event_only_while_visible()
    {
        var page = _f.Page();

        await page.Refresh(Url, Token);
        Assert.Equal((0, 0), (page.Drawn, _f.Server.SessionsCalls));

        page.SetShown(shown: true);
        Assert.Equal(1, page.Drawn);
        await page.Refresh(Url, Token);
        Assert.Equal(2, page.Drawn);

        page.SetShown(shown: false);
        await page.Refresh(Url, Token);
        Assert.Equal(2, page.Drawn);
    }

    [Fact]
    public async Task A_poll_re_words_the_ages_without_a_read_changing_them()
    {
        var page = await _f.Shown(Token);
        var row = page.Groups[0].Rows[0];
        var before = row.SinceText;

        _f.Clock.Advance(TimeSpan.FromMinutes(3));

        Assert.NotEqual(before, row.SinceText, StringComparer.Ordinal);
    }
}
