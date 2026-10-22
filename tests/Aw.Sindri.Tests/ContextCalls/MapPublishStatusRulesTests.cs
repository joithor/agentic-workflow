using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The publish state and its words (code index spec 6.4, "What the owner sees"): the six ids, the one stale sentence, and the cut to the caller's repos.</summary>
public sealed class MapPublishStatusRulesTests
{
    private static readonly DateTimeOffset _since = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _wait = TimeSpan.FromMinutes(15);

    private static MapPublishFacts Held(params PublishRepoFact[] repos) => new(14, _since, _wait, Current: 13, repos, Reason: null, ByHand: false);

    private static readonly string[] _fourRepos = ["aw-a", "aw-b", "aw-c", "aw-d"];

    private static MapPublishFacts Current(string? reason, bool byHand, params PublishRepoFact[] repos) => new(HeldVersion: null, default, TimeSpan.Zero, Current: 14, repos, reason, byHand);

    private static PublishRepoFact Waiting(string repo, RepoWait word = RepoWait.Building) => new(repo, "1234567", "abcdef0", Waiting: true, word);

    private static PublishRepoFact Repo(string repo, string? map, string? index) => new(repo, map, index, Waiting: false, RepoWait.None);

    [Fact, Trait("Lane", "unit")]
    public void A_map_inside_its_wait_is_held_and_names_the_repo_it_waits_on_and_when_the_wait_ends()
    {
        var view = MapPublishStatusRules.Of(Held(Waiting("aw-split-panes")), _since.AddMinutes(5));

        Assert.Equal(("held", false, 14, (DateTimeOffset?)(_since + _wait)), (view.State, view.Stale, view.Version, view.Until));
        Assert.Equal("Map v14 is held for the code index until 12:15Z.", view.Lines[0]);
        Assert.Equal("aw-split-panes: building (the map is for 1234567, the index is at abcdef0)", view.Lines[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_with_none_of_the_callers_repos_waiting_says_so()
    {
        var view = MapPublishStatusRules.Of(Held(Repo("aw-split-panes", "1234567", "1234567")), _since.AddMinutes(1));

        Assert.Equal("held", view.State);
        Assert.Equal(["Map v14 is held; none of your repos are waiting."], view.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_map_more_than_two_ticks_past_its_deadline_is_overdue_and_one_inside_two_ticks_is_not()
    {
        var late = MapPublishStatusRules.Of(Held(Waiting("aw-split-panes")), _since + _wait + TimeSpan.FromMinutes(3));
        var justLate = MapPublishStatusRules.Of(Held(Waiting("aw-split-panes")), _since + _wait + TimeSpan.FromMinutes(2));

        Assert.Equal("overdue", late.State);
        Assert.StartsWith("Map v14 is overdue: its wait ended at 12:15Z and it is still held", late.Lines[0], StringComparison.Ordinal);
        Assert.Equal("held", justLate.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_published_map_says_which_repos_match_and_which_are_not_indexed_yet()
    {
        var view = MapPublishStatusRules.Of(Current("IndexReady", byHand: false, Repo("aw-a", "1234567", "1234567abcdef"), Repo("aw-b", "7654321", index: null)), _since);

        Assert.Equal(("published", false), (view.State, view.Stale));
        Assert.Equal(["Map v14 is published; the code index matches for aw-a; not indexed yet: aw-b."], view.Lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("FirstMap", "it is the first map")]
    [InlineData("WaitOff", "waiting is turned off")]
    [InlineData("IndexBroken", "the code index is broken")]
    public void A_map_that_did_not_wait_for_the_index_says_why(string reason, string why)
    {
        var view = MapPublishStatusRules.Of(Current(reason, byHand: false, Repo("aw-a", "1234567", index: null)), _since);

        Assert.Equal(("published-without-waiting", reason), (view.State, view.Reason));
        Assert.Equal($"Map v14 was published without waiting for the code index ({why}).", view.Lines[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_a_person_promoted_is_published_by_hand()
    {
        var view = MapPublishStatusRules.Of(Current(reason: null, byHand: true, Repo("aw-a", "1234567", "1234567")), _since);

        Assert.Equal(("published-by-hand", "Map v14 was published by hand."), (view.State, view.Lines[0]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("TimedOut", "older than")]
    [InlineData("IndexReady", "newer than")]
    [InlineData(null, "not the commit of")]
    public void An_index_at_another_commit_than_the_map_is_stale_and_names_both_commits_the_index_first(string? reason, string relation)
    {
        var view = MapPublishStatusRules.Of(Current(reason, byHand: false, Repo("aw-a", "1234567", "abcdef0")), _since);

        Assert.Equal(("published-stale", true), (view.State, view.Stale));
        Assert.Equal([new StaleRepo("aw-a", "abcdef0", "1234567")], view.StaleRepos);
        Assert.Equal([$"Code answers are from abcdef0 ({relation} map v14, which is for 1234567)."], view.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stale_wins_over_how_the_map_was_published_and_clears_when_the_index_reaches_the_maps_commit()
    {
        var stale = MapPublishStatusRules.Of(Current("TimedOut", byHand: true, Repo("aw-a", "1234567", "abcdef0")), _since);
        var landed = MapPublishStatusRules.Of(Current("TimedOut", byHand: false, Repo("aw-a", "1234567", "1234567aaaaaaaa")), _since);

        Assert.Equal("published-stale", stale.State);
        Assert.Equal(("published", false), (landed.State, landed.Stale));
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_stale_repos_each_name_their_repo_and_after_two_the_rest_are_counted()
    {
        var repos = _fourRepos.Select(r => Repo(r, "1234567", "abcdef0")).ToArray();

        var view = MapPublishStatusRules.Of(Current("TimedOut", byHand: false, repos), _since);

        Assert.Equal(
            ["aw-a: Code answers are from abcdef0 (older than map v14, which is for 1234567).", "aw-b: Code answers are from abcdef0 (older than map v14, which is for 1234567).", "and 2 more"],
            view.Lines);
        Assert.Equal(4, view.StaleRepos.Count);
    }
}
