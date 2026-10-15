using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.MergeRun;

public sealed class MergeRunTests
{
    private readonly MetaWorld _world = new();

    private MergeRunService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo), _world.Repo, _world.Gates, _world.Log, _world.Clock);

    private async Task Ready(bool gated = true, bool mergeFails = false)
    {
        await _world.ReadyWithGate();
        if (gated)
        {
            GateStep[] steps = [new("sync-rules", GateVerdict.Pass, "/l/sync.log")];
            _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo"], steps, GateVerdict.Pass, A.At(5)), CancellationToken.None);
        }

        // origin/main is mainsha until the merge, newmainsha1234 after it.
        var merged = false;
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"headsha","isDraft":false,"title":"Add x","state":"OPEN"}""")
            .When("gh pr merge 190 --merge", _ =>
            {
                merged = !mergeFails;
                return new CommandResult(mergeFails ? 1 : 0, string.Empty, mergeFails ? "not mergeable" : string.Empty, TimedOut: false);
            })
            .When("rev-parse --verify --quiet origin/main", _ => new CommandResult(0, merged ? "newmainsha1234\n" : "mainsha\n", string.Empty, TimedOut: false))
            .When("git diff --name-only mainsha...headsha", "README.md\n")
            .When("git fetch");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_pull_request_is_merged_recorded_logged_and_announced()
    {
        await Ready();
        _ = await _world.Lanes.Save(new LaneMapState(1, [A.Assigned("api", "sindri-api")]), A.At(1), CancellationToken.None);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, "newmainsha1234", "main moved to newmain: #190 Add x merged. Merge main into your branches."), (result.Value.Merged, result.Value.Sha, result.Value.Announce));
        Assert.Equal(["gh pr merge 190 --merge"], _world.Runner.Lines.Where(l => l.StartsWith("gh pr merge", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Equal(new MergeRecord(190, "newmainsha1234", _world.Clock.GetUtcNow()), (await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Equal("- MERGE 2026-10-15 05:00: #190 Add x merged; main is now newmain", Assert.Single(_world.Log.Lines));
        var open = await _world.Open();
        Assert.Contains(open, o => o.Kind == ObligationKind.AnnounceMain && o.Key == "newmain");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_checks_refuse_is_not_merged()
    {
        await Ready(gated: false);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Merged);
        Assert.Contains(result.Value.Checks, c => c.Name == "gate" && !c.Ok);
        Assert.Null(result.Value.Announce);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh pr merge", StringComparison.Ordinal));
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_gh_merge_is_an_error_and_records_nothing()
    {
        await Ready(mergeFails: true);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new MergeRunValidator().Validate(new MergeRunRequest(-1)).Error.Code);
}
