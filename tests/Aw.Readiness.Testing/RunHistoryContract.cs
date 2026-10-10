using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The contract every <see cref="IRunHistory"/> passes (the in-memory fake and the JSON-file adapter).</summary>
public abstract class RunHistoryContract
{
    private static readonly RepoName _repo = RepoName.From("agentic-workflow");
    private static readonly RepoName _other = RepoName.From("other-repo");
    private static readonly DateTimeOffset _t0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A new, empty history.</summary>
    /// <returns>The history.</returns>
    protected abstract IRunHistory NewHistory();

    private static RunRecord Run(RepoName repo, int minutes, int level, CriterionState state) => new(
        repo, _t0.AddMinutes(minutes), level, [new RecordedState(CriterionId.From("SV-1"), state)]);

    /// <summary>An empty history has no latest run.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_history_has_no_latest_run()
    {
        var latest = await NewHistory().Latest(_repo, CancellationToken.None);

        Expect.True(latest.IsSuccess, "Latest failed on an empty history");
        Expect.Equal(expected: null, latest.Value, "Latest of an empty history");
    }

    /// <summary>A saved run comes back equal, field by field.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_saved_run_comes_back_as_the_latest()
    {
        var history = NewHistory();
        var run = Run(_repo, 0, 3, CriterionState.NotApplicable);

        var saved = await history.Save(run, CancellationToken.None);
        var latest = await history.Latest(_repo, CancellationToken.None);

        Expect.True(saved.IsSuccess && !string.IsNullOrWhiteSpace(saved.Value), "Save failed or named no location");
        Expect.True(latest.IsSuccess && latest.Value is not null, "Latest lost the saved run");
        Expect.Equal(run.Repo, latest.Value!.Repo, "Repo");   // why: checked non-null on the line above
        Expect.Equal(run.RecordedAt, latest.Value.RecordedAt, "RecordedAt");
        Expect.Equal(run.Level, latest.Value.Level, "Level");
        Expect.Sequence(run.States, latest.Value.States, "States");
    }

    /// <summary>Latest is the newest run of that repo, whatever the save order.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Latest_is_the_newest_run_of_that_repo()
    {
        var history = NewHistory();
        _ = await history.Save(Run(_repo, 10, 4, CriterionState.Pass), CancellationToken.None);
        _ = await history.Save(Run(_repo, 5, 2, CriterionState.Fail), CancellationToken.None);
        _ = await history.Save(Run(_other, 20, 1, CriterionState.Unknown), CancellationToken.None);

        var latest = await history.Latest(_repo, CancellationToken.None);

        Expect.True(latest.IsSuccess && latest.Value is not null, "Latest lost the saved runs");
        Expect.Equal(_t0.AddMinutes(10), latest.Value!.RecordedAt, "Latest is the newest run of the repo");   // why: checked non-null above
        Expect.Equal(4, latest.Value.Level, "Latest's level");
    }

    /// <summary>Two runs at the same instant are both kept.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Two_runs_at_the_same_instant_are_both_kept()
    {
        var history = NewHistory();

        var first = await history.Save(Run(_repo, 0, 1, CriterionState.Fail), CancellationToken.None);
        var second = await history.Save(Run(_repo, 0, 2, CriterionState.Pass), CancellationToken.None);

        Expect.True(first.IsSuccess && second.IsSuccess, "Save failed");
        Expect.True(!string.Equals(first.Value, second.Value, StringComparison.Ordinal), "the second run at the same instant overwrote the first");
    }
}
