using System.Text.Json;
using Aw.Context.Core.Eval.Backtest;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Text;
using static Aw.Sindri.Tests.Commands.JobRig;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>What 'context eval backtest' prints when its job ends (F5): the sample count line, one row per arm in report order, the pass line. Words only, no color.</summary>
public sealed class BacktestJobTextTests : IDisposable
{
    private static readonly string[] _jsonFlag = ["--json"];
    private readonly Commands.JobRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static ArmReport Ran(string arm, bool leaky, ArmScore score) => new(arm, "source", leaky, NotAvailable: null, score, new ArmPassLine("not met", "not met", "not met", "met", Verdicts.OwnerDecision), []);

    private static RunContextBacktestResult Result() => RunContextBacktestService.Summarize(new BacktestRun(new BacktestReport(1, new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero), "all", 5, SmallSample: true, 1.0,
        new LeftOut(1, 0, 0, ["SPL-106"]),
        [], [Ran("brief", leaky: false, new ArmScore(5, 5, 0.5, 0.4, 0.3, 1.0, 0)), Ran("docs", leaky: false, new ArmScore(5, 5, 0.25, NodePrecision: null, 0.1, 1.0, 12)),
            Ran("graph", leaky: false, new ArmScore(5, 5, 0.0, 0.0, 0.0, 1.0, 0)), Ran("graph+reviewed", leaky: true, new ArmScore(5, 5, 0.9, 0.8, 0.7, 1.0, 0)),
            Ran("all", leaky: false, new ArmScore(5, 5, 0.5, 0.4, 0.3, 1.0, 0)), new ArmReport("linear", "source", Leaky: false, "S-6", Score: null, PassLine: null, [])],
        Verdicts.OwnerDecision, ["clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"]), "/home/x/backtest.json"));

    private static readonly string _table = string.Join('\n',
        "5 samples · 1 left out · small sample",
        "  brief                  node recall 0.50 · node precision 0.40 · file recall@20 0.30 · leaky 1.00 · tokens 0",
        "  docs                   node recall 0.25 · node precision not measured · file recall@20 0.10 · leaky 1.00 · tokens 12",
        "  graph                  node recall 0.00 · node precision 0.00 · file recall@20 0.00 · leaky 1.00 · tokens 0",
        "  graph+reviewed (leaky) node recall 0.90 · node precision 0.80 · file recall@20 0.70 · leaky 1.00 · tokens 0",
        "  all                    node recall 0.50 · node precision 0.40 · file recall@20 0.30 · leaky 1.00 · tokens 0",
        "  linear                 not available (S-6)",
        "  pass line: owner decision needed (small sample)".TrimStart(),
        "degraded: clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)",
        "report /home/x/backtest.json",
        string.Empty);

    [Fact, Trait("Lane", "unit")]
    public void Summarize_carries_each_arms_numbers_and_leaky_label_and_stays_small()
    {
        var result = Result();

        Assert.Equal(new BacktestArmScore(5, 0.25, NodePrecision: null, 0.1, 1.0, 12), result.Arms[1].Score);
        Assert.True(result.Arms[3].Leaky);
        Assert.Null(result.Arms[5].Score);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(result).Length < 16 * 1024);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_job_result_renders_the_count_line_one_row_per_arm_in_order_and_the_pass_line() =>
        Assert.Equal(_table, BacktestJobText.Render(Result()).ReplaceLineEndings("\n"));

    private async Task<(int Exit, string Out)> Follow(string outcome, bool json)
    {
        var api = new Commands.JobApi().Ok("POST", "/sindri/context/eval/backtest", Body(Started))
            .Ok("GET", "/sindri/jobs/" + Id, View(outcome == "succeeded" ? JobState.Succeeded : JobState.Failed, rounds: 1, summary: "5 samples", result: JsonSerializer.SerializeToElement(Result(), SliceHarness.Json)));
        var run = Task.Run(async () => await new ContextJobVerbs(Client(api), _rig.Output(), _rig.Follower(api)).Run(["eval", "backtest", .. json ? _jsonFlag : []], _rig.Interrupted, _rig.Again));
        await api.Until(() => api.Streams == 1);
        await api.Frame(11, "job.finished", Finished(outcome, "5 samples"));
        return (await Finished(run), _rig.Out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_followed_backtest_prints_the_table_after_its_succeeded_line()
    {
        var (exit, text) = await Follow("succeeded", json: false);

        Assert.Equal(0, exit);
        Assert.Contains("succeeded: 5 samples\n" + _table, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_followed_backtest_with_json_prints_the_job_and_no_table()
    {
        var (exit, text) = await Follow("succeeded", json: true);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("node recall", text, StringComparison.Ordinal);
        Assert.Contains("\"report\":\"/home/x/backtest.json\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_backtest_prints_no_table()
    {
        var (exit, text) = await Follow("failed", json: false);

        Assert.Equal(1, exit);
        Assert.DoesNotContain("node recall", text, StringComparison.Ordinal);
    }
}
