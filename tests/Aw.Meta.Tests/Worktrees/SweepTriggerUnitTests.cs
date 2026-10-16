using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Watch;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Worktrees;

public sealed class SweepTriggerUnitTests
{
    private readonly MetaWorld _world = new();

    private sealed class FakeMerge(bool merged) : IService<MergeRunRequest, MergeRunResponse>
    {
        public ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(new MergeRunResponse(request.Pr, merged, [], merged ? "sha" : null, merged ? "announce" : null)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_the_checks_refused_triggers_no_sweep()
    {
        await _world.Ready();

        var result = await new SweepAfterMergeService(new FakeMerge(merged: false), _world.Config, _world.Sweeper).Handle(new MergeRunRequest(7), CancellationToken.None);

        Assert.False(result.Value.Merged);
        Assert.Null(result.Value.Worktrees);
        Assert.Empty(_world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_that_succeeded_stays_a_success_when_the_sweep_cannot_run()
    {
        await _world.Ready();   // no git or gh scripted: the sweep errors

        var result = await new SweepAfterMergeService(new FakeMerge(merged: true), _world.Config, _world.Sweeper).Handle(new MergeRunRequest(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((true, "sha", null), (result.Value.Merged, result.Value.Sha, result.Value.Worktrees));
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_without_a_team_config_is_still_returned_as_it_was()
    {
        var result = await new SweepAfterMergeService(new FakeMerge(merged: true), _world.Config, _world.Sweeper).Handle(new MergeRunRequest(7), CancellationToken.None);

        Assert.True(result.Value.Merged);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pull_requests_whose_sweep_could_not_run_stay_pending_for_the_next_poll()
    {
        await _world.Ready();
        var watch = new WatchSweeper(_world.Config, _world.Sweeper, _world.Values);

        var lines = await watch.Sweep([9], CancellationToken.None);

        Assert.Empty(lines);
        Assert.Equal("9", (await _world.Values.Get(KvKeys.WorktreesPending, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pending_pull_requests_are_cleared_once_a_sweep_runs_even_when_no_worktree_belongs_to_them()
    {
        await _world.Ready();
        _ = await _world.Values.Set(KvKeys.WorktreesPending, "9", CancellationToken.None);
        _ = _world.Runner.When("git fetch", string.Empty).When("rev-parse --verify --quiet origin/main", "m\n")
            .When("worktree list --porcelain", "worktree /work\nHEAD a\nbranch refs/heads/main\n").When("gh pr list", "[]").When("gh pr view", string.Empty, exitCode: 1);
        var watch = new WatchSweeper(_world.Config, _world.Sweeper, _world.Values);

        var lines = await watch.Sweep([], CancellationToken.None);

        Assert.Empty(lines);
        Assert.Equal(string.Empty, (await _world.Values.Get(KvKeys.WorktreesPending, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_nothing_ended_and_nothing_pending_the_watch_touches_nothing()
    {
        await _world.Ready();

        var lines = await new WatchSweeper(_world.Config, _world.Sweeper, _world.Values).Sweep([], CancellationToken.None);

        Assert.Empty(lines);
        Assert.Empty(_world.Runner.Lines);
    }

    private static PrView Pr(int number, PrState state) => new(Number: number, State: state, IsDraft: false, Head: "abc", Mergeable: string.Empty);

    [Fact, Trait("Lane", "unit")]
    public void Only_pull_requests_not_stored_as_ended_are_new()
    {
        PrView[] prs = [Pr(1, PrState.Merged), Pr(2, PrState.Open), Pr(3, PrState.Closed), Pr(4, PrState.Merged)];

        Assert.Equal([3, 4], EndedPrs.Added("1", prs));
        Assert.Equal([1, 3, 4], EndedPrs.Added(stored: null, prs));
        Assert.Empty(EndedPrs.Added("1,3,4", prs));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_worktree_settings_default_to_enabled_with_no_overrides_and_survive_a_config_round_trip()
    {
        var team = new TeamConfig("t", "/r", ["a"]);

        Assert.True(team.Worktrees.Enabled);
        Assert.Null(team.Worktrees.Roots);
        Assert.True(TeamConfigs.Same(TeamConfigs.Full(), TeamConfigs.Full()));
    }
}
