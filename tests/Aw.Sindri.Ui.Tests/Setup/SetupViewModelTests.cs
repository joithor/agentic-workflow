using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The Setup list as the card and the page read it: the six steps in words, the next one that needs the person, the three-row fold by priority, and what is shown for a list that can't be read.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupViewModelTests
{
    private readonly SetupFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Ids(IEnumerable<SetupStepViewModel> steps) => [.. steps.Select(s => s.Id)];

    [Fact]
    public async Task A_fresh_box_lists_the_six_steps_in_their_fixed_order_with_the_first_one_the_next()
    {
        _f.Serve("first-run");

        var setup = await _f.Read(Token);

        Assert.Equal(["repo", "profile", "recipes", "mcp", "map", "onyx"], Ids(setup.Steps));
        Assert.Equal(("repo", "0 of 6 done", false, false), (setup.Next?.Id, setup.Head, setup.IsReading, setup.IsFinished));
        Assert.True(setup.IsCardVisible);
    }

    [Fact]
    public async Task Each_step_reads_as_a_glyph_and_a_word_its_title_and_why()
    {
        _f.Serve("first-run");

        var steps = (await _f.Read(Token)).Steps;

        Assert.Equal(("Add your repo", "◆ YOUR TURN", "Pick the folder of a repo to start."), (steps[0].Title, steps[0].ChipText, steps[0].Why));
        Assert.Equal(("○ WAITING", "Waits for your first repo."), (steps[1].ChipText, steps[1].Why));
        Assert.Equal("○ NOT STARTED", steps[5].ChipText);   // a not-started Onyx waits for nothing and keeps its action
        Assert.Equal("Add your repo, your turn. Pick the folder of a repo to start.", steps[0].Name);
    }

    [Fact]
    public async Task Ids_and_codes_wait_in_Details_and_never_in_the_words_up_front()
    {
        _f.Serve("failed");

        var map = (await _f.Read(Token)).Steps.Single(s => s.Id == "map");

        Assert.Equal(("✕ FAILED", "The last try at building the map didn't finish."), (map.ChipText, map.ErrorText));
        Assert.DoesNotContain("SND-", map.Name + map.Why + map.ErrorText + map.ChipText, StringComparison.Ordinal);
        Assert.Contains(map.Fields, f => f is ("Error code", "SND-CONTEXT-042"));
        Assert.Contains(map.Fields, f => f is ("Step", "map"));
        Assert.Contains(map.Fields, f => f is ("State", "failed"));
    }

    [Fact]
    public async Task A_counted_step_shows_how_far_it_is_and_a_repo_step_one_line_per_repo()
    {
        _f.Serve("running-progress");

        var map = (await _f.Read(Token)).Steps.Single(s => s.Id == "map");

        Assert.Equal(("● WORKING", "40 of 100", "40 of 100 done."), (map.ChipText, map.ProgressText, map.DetailText));
        var line = Assert.Single(map.Items);
        Assert.Equal(("demo", "● WORKING"), (line.Repo, line.ChipText));
        Assert.Equal("demo, working", line.Name);
    }

    [Fact]
    public async Task The_card_draws_three_rows_filled_by_priority_then_says_how_many_it_leaves()
    {
        _f.Serve("extra-priority-mix");

        var setup = await _f.Read(Token);

        Assert.Equal(["mcp", "profile", "map"], Ids(setup.CardSteps));   // needs you, then failed, then running
        Assert.Equal(("and 3 more", true), (setup.MoreText, setup.HasMore));
        Assert.Equal(["map", "mcp", "profile"], setup.DrawnIds.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Three_running_steps_and_a_failed_Onyx_draw_the_failure_first_and_never_hide_it()
    {
        _f.Serve("extra-three-running-onyx-failed");

        var setup = await _f.Read(Token);

        Assert.Equal(["onyx", "recipes", "mcp"], Ids(setup.CardSteps));
        Assert.Equal("and 3 more", setup.MoreText);
    }

    [Fact]
    public async Task The_more_line_says_how_many_of_the_steps_it_hides_need_the_person()
    {
        _f.Serve(AllNeedYou());
        var setup = await _f.Read(Token);

        Assert.Equal(["repo", "profile", "recipes"], Ids(setup.CardSteps));
        Assert.Equal("and 3 more, 2 need you", setup.MoreText);
    }

    private static System.Text.Json.Nodes.JsonNode AllNeedYou()
    {
        var list = Golden("first-run");
        foreach (var step in list["steps"]!.AsArray())
        {
            step!["state"] = (string)step["id"]! == "onyx" ? "todo" : "needs-you";
        }

        return list;
    }

    [Fact]
    public async Task A_press_on_the_more_line_shows_every_step_and_another_folds_it_again()
    {
        _f.Serve("extra-priority-mix");
        var setup = await _f.Read(Token);

        setup.ToggleMore();
        var open = (Ids(setup.CardSteps).Length, setup.IsExpanded, setup.MoreText);
        setup.ToggleMore();

        Assert.Equal((6, true, "Show fewer"), open);
        Assert.Equal((3, false), (setup.CardSteps.Count, setup.IsExpanded));
    }

    [Fact]
    public async Task Waiting_steps_fill_the_card_in_step_order_after_the_one_that_needs_the_person()
    {
        _f.Serve("profile-needs-approval");

        var setup = await _f.Read(Token);

        Assert.Equal(["profile", "recipes", "mcp"], Ids(setup.CardSteps));   // needs you, then the todo steps in step order
    }

    [Fact]
    public async Task The_next_step_is_the_first_that_needs_the_person_with_its_action()
    {
        _f.Serve("profile-needs-approval");

        var setup = await _f.Read(Token);

        Assert.Equal(("profile", "ApproveProfile"), (setup.Next?.Id, setup.Next?.Action?.Action));
        Assert.Equal("Review and approve", setup.Next?.Action?.Label);
    }

    [Fact]
    public async Task A_failed_step_leads_when_nothing_needs_the_person()
    {
        _f.Serve("failed");

        var setup = await _f.Read(Token);

        Assert.Equal("map", setup.Next?.Id);
        Assert.Equal("This stopped", setup.LeadText);
    }

    [Theory]
    [InlineData("all-done")]
    [InlineData("onyx-no-docker")]
    public async Task Setup_is_finished_when_every_step_is_done_or_skipped_and_the_card_goes(string scenario)
    {
        _f.Serve(scenario);

        var setup = await _f.Read(Token);

        Assert.Equal((true, false, null, "Setup is finished."), (setup.IsFinished, setup.IsCardVisible, setup.Next, setup.LeadText));
        Assert.Empty(setup.DrawnIds);
    }

    [Fact]
    public async Task A_step_that_reopens_brings_the_card_back()
    {
        _f.Serve("all-done");
        var setup = await _f.Read(Token);
        _f.Serve(With("all-done", "map", "running"));

        await setup.Refresh(Url, Token);

        Assert.Equal((false, true), (setup.IsFinished, setup.IsCardVisible));
    }

    [Fact]
    public async Task A_repeat_read_keeps_each_step_and_changes_it_in_place()
    {
        _f.Serve("first-run");
        var setup = await _f.Read(Token);
        var repo = setup.Steps[0];
        _f.Serve("profile-needs-approval");

        await setup.Refresh(Url, Token);

        Assert.Same(repo, setup.Steps[0]);
        Assert.Equal("✓ DONE", repo.ChipText);
    }

    [Fact]
    public async Task A_state_this_build_does_not_know_renders_as_itself()
    {
        _f.Serve(With("first-run", "map", "paused"));

        var map = (await _f.Read(Token)).Steps.Single(s => s.Id == "map");

        Assert.Equal("○ PAUSED", map.ChipText);
    }

    [Fact]
    public async Task A_change_after_the_first_read_is_announced_once_in_words()
    {
        _f.Serve("first-run");
        var setup = await _f.Read(Token);
        Assert.False(setup.Live.IsAnnouncing);

        _f.Serve("profile-needs-approval");
        await setup.Refresh(Url, Token);

        Assert.Equal((true, "Add your repo: done. Approve the profile: your turn"), (setup.Live.IsAnnouncing, setup.Live.Announcement));
    }

    [Fact]
    public async Task The_first_read_announces_nothing()
    {
        _f.Serve("failed");

        var setup = await _f.Read(Token);

        Assert.False(setup.Live.IsAnnouncing);
    }

    [Fact]
    public async Task Before_the_first_answer_the_card_is_reading_and_hidden()
    {
        _f.Serve("first-run");
        var setup = _f.Setup();

        Assert.Equal((true, false), (setup.IsReading, setup.IsCardVisible));
        await setup.Refresh(Url, Token);
        Assert.Equal((false, true), (setup.IsReading, setup.IsCardVisible));
    }

    [Fact]
    public async Task A_sindri_without_the_route_says_so_and_draws_no_card()
    {
        _f.Server.SetupAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-API-004", "The API answered without an error code");

        var setup = await _f.Read(Token);

        Assert.Equal((true, false, false), (setup.IsNotServed, setup.IsCardVisible, setup.IsReading));
        Assert.Equal("This Sindri has no setup list yet.", setup.NotServedText);
    }

    [Fact]
    public async Task A_list_in_a_shape_this_build_cannot_read_is_unreadable()
    {
        _f.Server.SetupAnswer = new RouteAnswer(HttpStatusCode.OK, "{\"steps\":\"nope\"}");

        var setup = await _f.Read(Token);

        Assert.Equal((true, false), (setup.IsUnreadable, setup.IsCardVisible));
    }

    [Fact]
    public async Task When_nothing_answers_the_card_says_sindri_is_not_running_with_the_launcher_to_copy()
    {
        _f.Server.GoDown();

        var setup = await _f.Read(Token);

        Assert.Equal((true, false), (setup.IsNotRunning, setup.IsFailed));
        Assert.Equal(("Sindri isn't running.", "sindri-app", "The app reconnects on its own when Sindri starts."), (setup.NotRunningText, setup.Launcher?.Cli, setup.ReconnectsText));
        await setup.Launcher!.Copy(Token);
        Assert.Equal("sindri-app", _f.Clipboard.Text);
    }

    [Fact]
    public async Task A_failed_first_read_says_why_and_a_retry_reads_again()
    {
        _f.Server.SetupAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-005", "a setup source failed");
        var setup = await _f.Read(Token);
        Assert.Equal((true, false), (setup.IsFailed, setup.IsNotRunning));
        Assert.StartsWith("Couldn't read setup just now.", setup.FailedText, StringComparison.Ordinal);

        _f.Serve("first-run");
        await setup.Retry(Token);

        Assert.Equal((false, 6), (setup.IsFailed, setup.Steps.Count));
    }

    [Fact]
    public async Task A_failed_later_read_keeps_the_steps_and_marks_them_stale()
    {
        _f.Serve("first-run");
        var setup = await _f.Read(Token);
        _f.Server.GoDown();

        await setup.Refresh(Url, Token);

        Assert.Equal((6, true, false), (setup.Steps.Count, setup.IsStale, setup.IsFailed));
    }
}
