using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The publish state and its words (code index spec 6.4, "What the owner sees"): the six ids, the one stale sentence, and the cut to the caller's repos.</summary>
public sealed class MapPublishStatusRulesTests
{
    private static readonly DateTimeOffset _since = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _wait = TimeSpan.FromMinutes(15);

    private static MapPublishStatusView Rules(MapPublishFacts facts, DateTimeOffset now) => MapPublishStatusRules.Of(facts, now, TimeZoneInfo.Utc);

    private static MapPublishFacts Held(params PublishRepoFact[] repos) => new(14, _since, _wait, Current: 13, repos, Reason: null, ByHand: false);

    private static readonly string[] _fourRepos = ["aw-a", "aw-b", "aw-c", "aw-d"];

    private static MapPublishFacts Current(string? reason, bool byHand, params PublishRepoFact[] repos) => new(HeldVersion: null, default, TimeSpan.Zero, Current: 14, repos, reason, byHand);

    private static PublishRepoFact Waiting(string repo, RepoWait word = RepoWait.Building) => new(repo, "1234567", "abcdef0", Waiting: true, word);

    private static PublishRepoFact Repo(string repo, string? map, string? index) => new(repo, map, index, Waiting: false, RepoWait.None);

    [Fact, Trait("Lane", "unit")]
    public void A_map_inside_its_wait_is_held_and_names_the_repo_it_waits_on_and_when_the_wait_ends()
    {
        var view = Rules(Held(Waiting("aw-split-panes")), _since.AddMinutes(5));

        Assert.Equal(("held", false, 14, (DateTimeOffset?)(_since + _wait)), (view.State, view.Stale, view.Version, view.Until));
        Assert.Equal(
            [
                "Map v14 is ready. Waiting for the code index of aw-split-panes (building).",
                "It goes ahead by about 12:15 (your time) unless a newer build replaces it; v13 is current until then. To publish now: sindri context map promote.",
            ],
            view.Lines);
        Assert.Equal(["aw-split-panes (building)"], view.WaitingOn);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_with_none_of_the_callers_repos_waiting_says_so()
    {
        var view = Rules(Held(Repo("aw-split-panes", "1234567", "1234567")), _since.AddMinutes(1));

        Assert.Equal("held", view.State);
        Assert.Equal(["Map v14 is held; none of your repos are waiting."], view.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_more_than_two_ticks_past_its_deadline_is_overdue_and_one_inside_two_ticks_is_not()
    {
        var late = Rules(Held(Waiting("aw-split-panes")), _since + _wait + TimeSpan.FromMinutes(3));
        var justLate = Rules(Held(Waiting("aw-split-panes")), _since + _wait + TimeSpan.FromMinutes(2));

        Assert.Equal("overdue", late.State);
        Assert.Equal(
            "Map v14 should have gone ahead at 12:15. The map service is paused, or publishing is failing. v13 is current. Resume the map service or see the failing job; or sindri context map promote.",
            late.Lines[0]);
        Assert.Equal("held", justLate.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_published_map_says_which_repos_match_and_which_are_not_indexed_yet()
    {
        var view = Rules(Current("IndexReady", byHand: false, Repo("aw-a", "1234567", "1234567abcdef"), Repo("aw-b", "7654321", index: null)), _since);

        Assert.Equal(("published", false), (view.State, view.Stale));
        Assert.Equal(["Map v14 is current; the code index matches for aw-a (aw-a 1234567); not indexed yet: aw-b."], view.Lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("FirstMap", "Map v14 is the first map; the code index is not part of it yet.")]
    [InlineData("NoIndex", "Map v14 is current; there is no code index to wait for.")]
    [InlineData("WaitOff", "Map v14 is current; waiting for the code index is turned off.")]
    [InlineData("IndexBroken", "Map v14 is current; the code index was broken when it was published.")]
    public void A_map_that_did_not_wait_for_the_index_says_why(string reason, string sentence)
    {
        var view = Rules(Current(reason, byHand: false, Repo("aw-a", "1234567", index: null)), _since);

        Assert.Equal(("published-without-waiting", reason), (view.State, view.Reason));
        Assert.Equal(sentence, view.Lines[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_a_person_promoted_is_published_by_hand()
    {
        var view = Rules(Current(reason: null, byHand: true, Repo("aw-a", "1234567", "1234567")), _since);

        Assert.Equal(("published-by-hand", "Map v14 was published by hand."), (view.State, view.Lines[0]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("TimedOut", "older than")]
    [InlineData("IndexReady", "newer than")]
    [InlineData(null, "not the commit of")]
    public void An_index_at_another_commit_than_the_map_is_stale_and_names_both_commits_the_index_first(string? reason, string relation)
    {
        var view = Rules(Current(reason, byHand: false, Repo("aw-a", "1234567", "abcdef0")), _since);

        Assert.Equal(("published-stale", true), (view.State, view.Stale));
        Assert.Equal([new StaleRepo("aw-a", "abcdef0", "1234567")], view.StaleRepos);
        Assert.Equal($"Code answers are from abcdef0 ({relation} map v14, which is for 1234567).", view.Lines[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stale_wins_over_how_the_map_was_published_and_clears_when_the_index_reaches_the_maps_commit()
    {
        var stale = Rules(Current("TimedOut", byHand: true, Repo("aw-a", "1234567", "abcdef0")), _since);
        var landed = Rules(Current("TimedOut", byHand: false, Repo("aw-a", "1234567", "1234567aaaaaaaa")), _since);

        Assert.Equal("published-stale", stale.State);
        Assert.Equal(("published", false), (landed.State, landed.Stale));
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_stale_repos_each_name_their_repo_and_after_two_the_rest_are_counted()
    {
        var repos = _fourRepos.Select(r => Repo(r, "1234567", "abcdef0")).ToArray();

        var view = Rules(Current("TimedOut", byHand: false, repos), _since);

        Assert.Equal(
            ["aw-a: Code answers are from abcdef0 (older than map v14, which is for 1234567).", "aw-b: Code answers are from abcdef0 (older than map v14, which is for 1234567).", "and 2 more", "The code index did not finish in time."],
            view.Lines);
        Assert.Equal(4, view.StaleRepos.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_whose_index_has_caught_up_after_a_timed_out_wait_says_so()
    {
        var view = Rules(Current("TimedOut", byHand: false, Repo("aw-a", "1234567", "1234567aaaa")), _since);

        Assert.Equal(("published", "Map v14 waited out its wait; the code index has caught up."), (view.State, view.Lines[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_matching_single_repo_reads_the_code_index_matches_with_its_commit()
    {
        var view = Rules(Current("IndexReady", byHand: false, Repo("aw-a", "1234567", "1234567aaaa")), _since);

        Assert.Equal(["Map v14 is current; the code index matches (1234567)."], view.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stale_sentence_says_when_the_index_was_built_and_a_broken_index_adds_where_to_look()
    {
        var repo = new PublishRepoFact("aw-a", "1234567", "abcdef0", Waiting: false, RepoWait.None, BuiltAt: _since.AddMinutes(-12));

        var view = Rules(Current("IndexBroken", byHand: false, repo), _since);

        Assert.Equal(["Code answers are from abcdef0 (built 12 min ago, older than map v14, which is for 1234567).", "The code index is broken, see Code index."], view.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_names_the_retry_time_the_absent_repos_and_the_repos_whose_index_is_already_newer()
    {
        var facts = Held(
            new PublishRepoFact("web", "1234567", "abcdef0", Waiting: true, RepoWait.Retrying, RetryAt: _since.AddMinutes(17)),
            Repo("web2", "1234567", index: null) with { Word = RepoWait.NotIndexed },
            Repo("api", "1234567", "9999999") with { Word = RepoWait.NewerIndex });

        var view = Rules(facts, _since.AddMinutes(5));

        Assert.Equal("Map v14 is ready. Waiting for the code index of web (retrying in 12 min).", view.Lines[0]);
        Assert.Equal("web2 is not indexed yet and is not waited for.", view.Lines[1]);
        Assert.Equal("Code answers for api are already from the newer commit.", view.Lines[2]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_drop_that_is_the_newest_map_event_adds_the_held_map_was_replaced_line()
    {
        var view = Rules(Current("IndexReady", byHand: false, Repo("aw-a", "1234567", "1234567")) with { DroppedVersion = 15 }, _since);

        Assert.Equal("Held map v15 was replaced by a newer build.", view.Lines[0]);
        Assert.Equal("published", view.State);
    }
}
