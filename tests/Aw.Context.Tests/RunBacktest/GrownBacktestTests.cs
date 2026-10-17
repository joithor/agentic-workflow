using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.RunBacktest;

public sealed class GrownBacktestTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_on_grown_corpus_reports_a_pass_line_per_arm()
    {
        using var grown = GrownCorpus.Build();
        await using var world = await BacktestWorld.Start(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), grown);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        Assert.Equal((32, false), (report.SampleCount, report.SmallSample));
        Assert.Equal(32, grown.Repo.PullRequests.Count);
        var judged = report.Arms.Where(a => a.PassLine is not null).ToList();
        Assert.Equal(["brief", "docs", "graph", "graph+reviewed", "all"], judged.Select(a => a.Arm), StringComparer.Ordinal);
        Assert.All(judged, a => Assert.Contains(a.PassLine!.Overall, (string[])[Verdicts.Pass, Verdicts.NotPassed, Verdicts.NotMeasured], StringComparer.Ordinal));   // never the small-sample line
        Assert.Equal(1.0, report.LeakyShare);   // every brief was edited after its cut
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Grown_corpus_is_deterministic_synthetic_and_every_merge_changes_a_file()
    {
        using var first = GrownCorpus.Build();
        using var second = GrownCorpus.Build();

        Assert.Equal(first.Repo.Head, second.Repo.Head);   // pinned dates, so the shas repeat
        Assert.All(first.Repo.PullRequests, p => Assert.NotEmpty(p.Files));
        Assert.Equal(Enumerable.Range(201, 32).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SPL-{i}")), first.Issues.Select(i => i.Key), StringComparer.Ordinal);
        Assert.All(first.Issues, i => Assert.True(i.UpdatedAt > i.CreatedAt));
        Assert.DoesNotContain(first.Issues.SelectMany(i => i.Chunks), c => c.Contains('@', StringComparison.Ordinal));
        await Task.CompletedTask;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_golden_report_on_split_panes()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        Golden.Match(Serialize(report), typeof(GrownBacktestTests).Assembly, "RunBacktest/Golden/backtest-split-panes.json", Normalization.None);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_golden_report_on_the_grown_corpus()
    {
        using var grown = GrownCorpus.Build();
        await using var world = await BacktestWorld.Start(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), grown);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        Golden.Match(Serialize(report), typeof(GrownBacktestTests).Assembly, "RunBacktest/Golden/backtest-grown.json", Normalization.None);
    }

    private static string Serialize(BacktestReport report) =>
        System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(report, Eval.EvalJson.Options)) + "\n";
}
