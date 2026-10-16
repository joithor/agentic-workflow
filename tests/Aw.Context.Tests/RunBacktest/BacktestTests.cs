using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.RunBacktest;

public sealed class BacktestTests
{
    private static ArmReport Arm(BacktestReport report, string name) => report.Arms.Single(a => a.Arm == name);

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_on_split_panes_prints_five_samples_small_sample_and_owner_decision()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var run = (await world.Run()).Value;

        var report = run.Report;
        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105"], report.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal((5, true, 1), (report.SampleCount, report.SmallSample, report.LeftOut.NoMerge));
        Assert.Equal(["SPL-106"], report.LeftOut.Keys);   // no merged PR
        Assert.Equal(1.0, report.LeakyShare);   // every fixture issue was edited after its cut
        Assert.Equal(["brief", "docs", "graph", "graph+reviewed", "all", "linear", "onyx", "code"], report.Arms.Select(a => a.Arm), StringComparer.Ordinal);
        Assert.Equal(["S-6", "S-7B", "S-9"], report.Arms.Select(a => a.NotAvailable).OfType<string>(), StringComparer.Ordinal);
        Assert.All(report.Arms.Where(a => a.NotAvailable is null), a => Assert.Equal(Verdicts.OwnerDecision, a.PassLine!.Overall));
        Assert.Equal(Verdicts.OwnerDecision, report.Overall);
        Assert.StartsWith(world.Settings.BacktestDirectory, run.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(run.Path));
        var cut = report.Samples.ToDictionary(s => s.IssueKey, s => s.Cut, StringComparer.Ordinal);
        Assert.Equal(cut["SPL-101"], cut["SPL-102"]);   // both cut at the initial commit
        Assert.NotEqual(cut["SPL-103"], cut["SPL-104"], StringComparer.Ordinal);
        Assert.Equal(report.Samples.Select(s => s.IssueKey), Arm(report, "graph").Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_graph_reviewed_arm_reads_current_map_and_is_labeled_leaky()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        var ceiling = Arm(report, "graph+reviewed");
        Assert.True(ceiling.Leaky);
        Assert.Equal(1.0, ceiling.Score!.LeakyShare);
        Assert.All(ceiling.Samples, s => Assert.True(s.Leaky));
        Assert.False(Arm(report, "graph").Leaky);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_makes_no_model_call_with_adjudicator_off()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        Assert.Empty(world.Model.Requests);
        Assert.All(report.Arms.Where(a => a.Score is not null), a => Assert.Equal(0, a.Score!.Tokens));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_takes_one_heavy_slot_and_busy_lock_is_040()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        _ = await world.Run("graph");
        Assert.Equal((1, 0), (world.HeavyLock.Taken, world.HeavyLock.Held));   // one slot for the whole run, released after

        world.HeavyLock.BusyHolder = "pid 4242";
        var busy = await world.Run("graph");

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, busy.Error.Code);
        Assert.Contains("pid 4242", busy.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_session_caller_is_020()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var refused = await world.Run("graph", world.Session());

        Assert.Equal(ContextErrors.SessionsDisabled.Code, refused.Error.Code);
        Assert.Contains("human or service job", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_without_brief_source_is_081_and_with_no_eligible_sample_is_080()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var none = await world.Run("graph", world.Human(onyx: false));
        var empty = await world.Run("graph", configure: r => r with { Since = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) });

        Assert.Equal(ContextErrors.NoBriefSource.Code, none.Error.Code);
        Assert.Equal(ContextErrors.NoEligibleSample.Code, empty.Error.Code);
        Assert.Contains("5", empty.Error.Message, StringComparison.Ordinal);   // the counts: what the window cut away is named
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_window_and_limit_cut_the_samples()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var until = (await world.Run("graph", configure: r => r with { Until = new DateTimeOffset(2026, 7, 2, 23, 0, 0, TimeSpan.Zero) })).Value.Report;
        var limited = (await world.Run("graph", configure: r => r with { Limit = 2 })).Value.Report;

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103"], until.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal(["SPL-101", "SPL-102"], limited.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.True(limited.SmallSample);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_cancelled_writes_no_report()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await world.RunWith(cancel.Token, "graph"));

        Assert.False(Directory.Exists(world.Settings.BacktestDirectory) && Directory.EnumerateFiles(world.Settings.BacktestDirectory).Any());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_extract_failure_at_a_cut_drops_the_sample_with_048_counted()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts, wrap: inner => new FailingReader(inner));
        await world.ImportMap();

        var report = (await world.Run("graph")).Value.Report;

        Assert.DoesNotContain(report.Samples, s => s.IssueKey == "SPL-104");
        Assert.Equal(1, report.LeftOut.ExtractFailed);
        Assert.Equal(4, report.SampleCount);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_report_holds_refs_and_numbers_never_brief_text()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var run = (await world.Run()).Value;

        var text = await File.ReadAllTextAsync(run.Path, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("named command in one registry", text, StringComparison.Ordinal);   // SPL-101's brief
        Assert.DoesNotContain("Command registry for pane commands", text, StringComparison.Ordinal);   // and its title
        Assert.Contains("onyx/0e5a1b00-0000-4000-8000-000000000101", text, StringComparison.Ordinal);   // the ref is there
        Assert.DoesNotContain(world.Onyx.Token, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_is_deterministic_and_a_second_run_reads_the_map_cache()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        var first = new ListProgress();
        var second = new ListProgress();

        var one = (await world.Run("all", configure: r => r with { Progress = first })).Value.Report;
        var two = (await world.Run("all", configure: r => r with { Progress = second })).Value.Report;

        Assert.Equal(one.Arms.Select(a => a.Score), two.Arms.Select(a => a.Score));
        Assert.Contains("corpus: 5 samples", first.Lines, StringComparer.Ordinal);
        Assert.Contains("sample 1/5 SPL-101: map built", first.Lines, StringComparer.Ordinal);
        Assert.Contains("sample 3/5 SPL-103: map cached", first.Lines, StringComparer.Ordinal);   // SPL-101..103 share the initial commit as their cut
        Assert.All(second.Lines.Where(l => l.StartsWith("sample ", StringComparison.Ordinal) && l.Contains(": map", StringComparison.Ordinal)), l => Assert.EndsWith("map cached", l, StringComparison.Ordinal));
        Assert.Contains(first.Lines, l => l.StartsWith("arm graph SPL-103", StringComparison.Ordinal));
    }
}
