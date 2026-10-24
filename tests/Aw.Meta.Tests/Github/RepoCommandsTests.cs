using Aw.Meta.Core;
using Aw.Meta.Core.Watch;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Github;

public sealed class RepoCommandsTests
{
    private readonly MetaWorld _world = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_view_asks_gh_in_the_repo_and_reads_the_json()
    {
        _ = _world.Runner.When("gh pr view 190", """{"headRefOid":"abc","title":"T","isDraft":false,"state":"OPEN"}""");

        var view = await _world.Repo.View("/work", 190, "headRefOid,title");

        Assert.Equal("abc", view.Value.Head);
        var request = Assert.Single(_world.Runner.Requests);
        Assert.Equal(["gh", "pr", "view", "190", "--json", "headRefOid,title"], request.Argv);
        Assert.Equal("/work", request.Cwd);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_exits_non_zero_is_AW_META_012_with_its_first_line()
    {
        _ = _world.Runner.When("gh pr view", "", exitCode: 1);

        var view = await _world.Repo.View("/work", 190, "state");

        Assert.Equal(MetaErrors.CommandFailed.Code, view.Error.Code);
        Assert.Contains("failed: gh pr view", view.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Output_that_is_not_what_gh_prints_is_AW_META_012()
    {
        _ = _world.Runner.When("gh pr list", "not json");

        var list = await _world.Repo.List("/work");

        Assert.Equal(MetaErrors.CommandFailed.Code, list.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_list_asks_for_every_state_and_reads_the_pull_requests()
    {
        _ = _world.Runner.When("gh pr list --state all --limit 50 --json number,state,isDraft,headRefOid,mergeable,title,updatedAt", """[{"number":5,"state":"MERGED","headRefOid":"h"}]""");

        var list = await _world.Repo.List("/work");

        Assert.Equal([new PrView(5, PrState.Merged, IsDraft: false, "h", string.Empty)], list.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Fetch_gets_origin_and_then_the_pull_request_head()
    {
        _ = _world.Runner.When("git fetch");

        var both = await _world.Repo.Fetch("/work", 190);
        var originOnly = await _world.Repo.Fetch("/work", pr: null);

        Assert.True(both.IsSuccess && originOnly.IsSuccess);
        Assert.Equal(["git fetch --quiet origin", "git fetch --quiet origin pull/190/head", "git fetch --quiet origin"], _world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_paths_and_the_remote_main_are_read_from_git()
    {
        _ = _world.Runner.When("git diff --name-only origin/main...abc", "src/a.cs\ndocs/b.md\n");
        _ = _world.Runner.When("git ls-remote origin main", "0123456789abcdef\trefs/heads/main\n");

        var changed = await _world.Repo.Changed("/work", "origin/main...abc");
        var main = await _world.Repo.RemoteMain("/work");

        Assert.Equal(["src/a.cs", "docs/b.md"], changed.Value);
        Assert.Equal("0123456789abcdef", main.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_command_may_run_for_fifteen_minutes()
    {
        _ = _world.Runner.When("gh pr view 190", """{"headRefOid":"abc","title":"T","isDraft":false,"state":"OPEN"}""");

        _ = await _world.Repo.View("/work", 190, "headRefOid");

        Assert.Equal(TimeSpan.FromMinutes(15), Assert.Single(_world.Runner.Requests).Timeout);
    }
}
