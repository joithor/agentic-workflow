using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Stats;
using static Aw.Sindri.Tests.GetRecipeStats.StatRows;

namespace Aw.Sindri.Tests.PrBabysitter;

/// <summary>The babysitter's own figures (PR babysitter spec API19), counted from ledger transitions and merge records.</summary>
[Lane(TestLane.Unit)]
public sealed class BabysitterStatsCounterTests
{
    private const string A = "T-1/pr-babysitter/1";
    private const string B = "T-2/pr-babysitter/1";
    private const string Recipe = "workflow.pr-babysitter";
    private static readonly DateTimeOffset _base = Ago(1);

    private static readonly IReadOnlyDictionary<string, MergeRecordRows> _noRecords = new Dictionary<string, MergeRecordRows>(StringComparer.Ordinal);

    private static StatEvent Begin(string address, DateTimeOffset? at = null) => Start(address, at ?? _base, state: "intake", recipe: Recipe);

    private static StatEvent At(string address, int minutes, string from, string to, int? tokens = null) => Move(address, _base.AddMinutes(minutes), from, to, tokens);

    private static BabysitterStats Count(IReadOnlyList<StatEvent> rows, IReadOnlyDictionary<string, MergeRecordRows>? records = null, StatsWindow? window = null) =>
        BabysitterStatsCounter.Count(rows, records ?? _noRecords, window ?? StatsWindow.ThirtyDays, Now);

    private static MergeRowRead Row(string kind, params (string Name, string? Value)[] fields)
    {
        var json = new JsonObject { ["kind"] = kind };
        foreach (var (name, value) in fields)
        {
            json[name] = value;
        }

        return new MergeRowRead(kind, json);
    }

    [Fact]
    public void Times_and_tokens_run_from_the_start_over_the_instances_that_got_there()
    {
        var stats = Count(
        [
            Begin(A), At(A, 10, "snapshot", "ready", tokens: 1000), At(A, 20, "ready", "merging"), At(A, 30, "merging", "merged", tokens: 3000),
            Begin(B), At(B, 30, "snapshot", "ready", tokens: 5000),
        ]);

        Assert.Equal(2, stats.Pulls);
        Assert.Equal(new BabysitterSpread(1200, 1800), stats.SinceStartToReadySeconds);
        Assert.Equal(new BabysitterSpread(1800, 1800), stats.SinceStartToMergeSeconds);
        Assert.Equal(new BabysitterSpread(4000, 5000), stats.TokensPerPull);
    }

    [Fact]
    public void An_instance_with_no_rows_has_no_figures_and_no_instances_is_all_zero()
    {
        var stats = Count([]);

        Assert.Equal(0, stats.Pulls);
        Assert.Equal(new BabysitterSpread(Median: null, P90: null), stats.SinceStartToReadySeconds);
        Assert.Equal(new CiFixStats(Attempts: 0, Succeeded: 0, StillRed: 0, Rate: null), stats.CiFix);
    }

    [Fact]
    public void The_owners_actions_at_stop_3_are_the_transitions_out_of_ready_by_where_they_go()
    {
        var stats = Count(
        [
            Begin(A),
            At(A, 1, "ready", "merging"), At(A, 2, "ready", "rework-notes"), At(A, 3, "ready", "held-next"), At(A, 4, "ready", "held-next"),
            At(A, 5, "ready", "docs"), At(A, 6, "ready", "snapshot"), At(A, 7, "ready", "ready-check"),
        ]);

        Assert.Equal(new OwnerActionStats(Merge: 1, Rework: 1, Comments: 2, ResyncDocs: 1, Check: 1), stats.OwnerActionsAtReady);
    }

    [Fact]
    public void A_CI_fix_succeeds_when_the_next_snapshot_does_not_send_it_back_and_is_open_with_no_next_snapshot()
    {
        var stats = Count(
        [
            Begin(A),
            At(A, 1, "snapshot", "ci-fix"), At(A, 2, "ci-fix", "pushing"), At(A, 3, "pushing", "snapshot"),
            At(A, 4, "snapshot", "ci-fix"), At(A, 5, "ci-fix", "pushing"), At(A, 6, "pushing", "snapshot"),
            At(A, 7, "snapshot", "ready"),
            At(A, 8, "snapshot", "ci-fix"),
        ]);

        Assert.Equal(new CiFixStats(Attempts: 3, Succeeded: 1, StillRed: 1, Rate: 0.5), stats.CiFix);
    }

    [Fact]
    public void Rechecks_count_every_entry_and_how_each_verdict_left()
    {
        var stats = Count(
        [
            Begin(A),
            At(A, 1, "snapshot", "recheck"), At(A, 2, "recheck", "snapshot"),
            At(A, 3, "snapshot", "recheck"), At(A, 4, "recheck", "recheck-owner"), At(A, 5, "recheck-owner", "recheck"),
        ]);

        Assert.Equal(new RecheckStats(Runs: 3, Resolved: 1, Unresolved: 1), stats.Rechecks);
    }

    [Fact]
    public void Docs_pull_requests_count_by_transition_and_by_the_records_outcome()
    {
        var records = new Dictionary<string, MergeRecordRows>(StringComparer.Ordinal)
        {
            [A] = new([Row("docsMerge", ("outcome", "merged")), Row("docsMerge", ("outcome", "left")), Row("snapshot")], 0),
            [B] = new([Row("docsMerge", ("outcome", "merged"))], 0),
        };

        var stats = Count([Begin(A), At(A, 1, "snapshot", "docs-merging"), At(A, 2, "snapshot", "docs-merge-owner"), Begin(B)], records);

        Assert.Equal((1, 1), (stats.Docs.WithMerge, stats.Docs.OwnerDesk));
        Assert.Equal([new ReasonCount("merged", 2), new ReasonCount("left", 1)], stats.Docs.ByOutcome);
    }

    [Fact]
    public void Comments_count_by_judge_type_and_outcome_and_split_held_from_handled_without_the_owner()
    {
        var records = new Dictionary<string, MergeRecordRows>(StringComparer.Ordinal)
        {
            [A] = new(
            [
                Row("comment", ("type", "nit"), ("outcome", "fixed")), Row("comment", ("type", "nit"), ("outcome", "fixed")), Row("comment", ("type", "directive"), ("outcome", "replied")),
                Row("comment", ("type", "design"), ("outcome", "held")), Row("comment", ("type", null), ("outcome", "reacted")), Row("comment", ("type", "design"), ("outcome", "skipped")),
            ], 0),
        };

        var comments = Count([Begin(A)], records).Comments;

        Assert.Equal((6, 1, 5), (comments.Total, comments.Held, comments.WithoutOwner));
        Assert.Equal(
            [new CommentTypeStats("design", 0, 0, 0, 1, 1), new CommentTypeStats("directive", 0, 1, 0, 0, 0), new CommentTypeStats("nit", 2, 0, 0, 0, 0), new CommentTypeStats("none", 0, 0, 1, 0, 0)],
            comments.ByType);
    }

    [Fact]
    public void Record_errors_add_up_across_instances_and_other_recipes_and_old_instances_are_left_out()
    {
        var records = new Dictionary<string, MergeRecordRows>(StringComparer.Ordinal) { [A] = new([], 2), [B] = new([], 1), ["T-9/other/1"] = new([], 5), ["T-8/pr-babysitter/1"] = new([], 7) };
        var rows = new List<StatEvent>
        {
            Begin(A), Begin(B), Start("T-9/other/1", _base, recipe: "workflow.other"), Begin("T-8/pr-babysitter/1", Ago(40)),
        };

        var stats = Count(rows, records);

        Assert.Equal((2, 3), (stats.Pulls, stats.RecordErrors));
    }
}
