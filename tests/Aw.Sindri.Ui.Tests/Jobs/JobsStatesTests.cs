using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs page's filters and the line above a table with nothing running (F3 jobs spec §3).</summary>
[Lane(TestLane.Unit)]
public sealed class JobsStatesTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Choosing_a_status_reads_the_list_with_the_served_state()
    {
        var page = await _f.Shown(Token);

        page.SelectedState = "Done";
        await page.CatchUp;

        Assert.Equal("succeeded", _f.Server.LastJobsQuery?.State);
        Assert.Null(_f.Server.LastJobsQuery?.Kind);
    }

    [Fact]
    public async Task Choosing_a_kind_reads_the_list_with_the_served_kind()
    {
        var page = await _f.Shown(Token);
        Assert.Contains("Echo job", page.KindChoices, StringComparer.Ordinal);

        page.SelectedKind = "Echo job";
        await page.CatchUp;

        Assert.Equal("EchoJob", _f.Server.LastJobsQuery?.Kind);
    }

    [Fact]
    public async Task All_states_is_no_filter()
    {
        var page = await _f.Shown(Token);
        page.SelectedState = "Failed";
        await page.CatchUp;

        page.SelectedState = page.StateChoices[0];
        await page.CatchUp;

        Assert.Null(_f.Server.LastJobsQuery?.State);
    }

    [Fact]
    public async Task A_table_of_ended_jobs_says_nothing_is_running()
    {
        _f.ServeList("by-kind");

        var page = await _f.Shown(Token);

        Assert.Equal((true, "No running jobs."), (page.IsNoneRunning, page.NoneRunningText));
    }

    [Fact]
    public async Task A_running_job_means_no_such_line()
    {
        var page = await _f.Shown(Token);

        Assert.False(page.IsNoneRunning);
    }

    [Fact]
    public async Task Nothing_run_yet_offers_the_link_to_Services()
    {
        _f.ServeList("empty-unfiltered");

        var page = await _f.Shown(Token);

        Assert.Equal((true, "See Sindri's services"), (page.ShowServicesLink, page.ServicesLinkText));
    }

    [Fact]
    public async Task A_filtered_empty_list_has_no_Services_link()
    {
        var page = await _f.Shown(Token);
        _f.ServeList("empty");

        page.SelectedState = "Running";
        await page.CatchUp;

        Assert.Equal((true, false), (page.IsEmpty, page.ShowServicesLink));
    }

    [Fact]
    public async Task The_Services_link_asks_the_shell_for_Services()
    {
        var page = await _f.Shown(Token);
        var asked = 0;
        page.ServicesRequested += (_, _) => asked++;

        page.RequestServices();

        Assert.Equal(1, asked);
    }

    [Fact]
    public void Before_the_first_answer_six_skeleton_rows_stand_in_for_the_table()
    {
        var page = _f.Page();

        Assert.Equal((true, 6), (page.IsReading, page.SkeletonRows.Count));
    }

    [Fact]
    public async Task A_failed_reload_dims_the_old_table_and_offers_Try_again()
    {
        var page = await _f.Shown(Token);
        _f.Server.JobsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");

        await page.Refresh(Url, Token);

        Assert.Equal((1, true, true), (page.Rows.Count, page.IsDimmed, page.ShowRetry));
        Assert.StartsWith("Couldn't read the jobs just now.", page.FailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Try_again_after_a_failed_reload_brings_the_table_back()
    {
        var page = await _f.Shown(Token);
        _f.Server.JobsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views have not caught up");
        await page.Refresh(Url, Token);
        _f.ServeList("running");

        await page.Retry(Token);

        Assert.Equal((false, false), (page.IsDimmed, page.ShowRetry));
    }

    [Fact]
    public async Task An_unreadable_answer_is_not_a_retry()
    {
        var page = await _f.Shown(Token);
        _f.Server.JobsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, "[]");

        await page.Refresh(Url, Token);

        Assert.False(page.ShowRetry);
    }

    [Theory]
    [InlineData(759, true)]
    [InlineData(760, false)]
    [InlineData(1440, false)]
    public async Task Under_760_px_the_table_keeps_only_Job_Status_and_Rounds(double width, bool compact)
    {
        var page = await _f.Shown(Token);

        page.Resize(width);

        Assert.Equal((compact, compact), (page.IsCompact, page.Rows[0].IsCompact));
    }

    [Fact]
    public async Task A_row_that_arrives_after_the_page_narrowed_is_compact_too()
    {
        var page = await _f.Shown(Token);
        page.Resize(500);

        await page.Refresh(Url, Token);
        _f.ServeList("by-kind");
        await page.Refresh(Url, Token);

        Assert.All(page.Rows, r => Assert.True(r.IsCompact));
    }
}
