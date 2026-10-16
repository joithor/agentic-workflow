using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Text;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextEvalVerbsTests
{
    private static BacktestReport Report() => new(1, new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero), "all", 5, SmallSample: true, 1.0, new LeftOut(1, 0, 0, ["SPL-106"]),
        [], [Row("brief", leaky: false), Row("graph+reviewed", leaky: true), Pending("linear", "S-6"), Pending("onyx", "S-7B"), Pending("code", "S-9")],
        Verdicts.OwnerDecision, []);

    private static ArmReport Pending(string arm, string stage) => new(arm, "source", Leaky: false, stage, Score: null, PassLine: null, []);

    private static ArmReport Row(string arm, bool leaky) =>
        new(arm, "source", leaky, NotAvailable: null, new ArmScore(5, 5, 0.5, 0.4, 0.3, 1.0, 0), new ArmPassLine("not met", "not met", "not met", "met", Verdicts.OwnerDecision), []);

    [Fact, Trait("Lane", "unit")]
    public void Eval_cli_backtest_text_lists_arms_in_order_with_not_available_rows()
    {
        var text = ContextEvalText.Backtest(Report(), "/home/x/backtest.json");

        Assert.StartsWith("5 samples", text, StringComparison.Ordinal);
        Assert.Contains("left out: SPL-106 (1 with no merged PR)", text, StringComparison.Ordinal);
        Assert.Contains("small sample", text, StringComparison.Ordinal);
        string[] order = ["  brief ", "  graph+reviewed (leaky)", "  linear ", "  onyx ", "  code "];
        Assert.Equal(order, order.OrderBy(o => text.IndexOf(o, StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Contains("linear          not available (S-6)", text, StringComparison.Ordinal);
        Assert.Contains("pass line: " + Verdicts.OwnerDecision, text, StringComparison.Ordinal);
        Assert.Contains("report /home/x/backtest.json", text, StringComparison.Ordinal);
    }

    private static async Task<(int Code, string Out)> Verb(params string[] args)
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json);
        var code = await new ContextEvalVerbs(client, new Output(stdout, stderr, SliceHarness.Json), FakeContextEngine.Instance).Run(args, TestContext.Current.CancellationToken);
        return (code, stdout + stderr.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_backtest_is_pending_API_P6_but_checks_the_arms_first()
    {
        var (_, pending) = await Verb("backtest", "--arms", "all");
        var (badCode, bad) = await Verb("backtest", "--arms", "bogus");
        var (_, ranker) = await Verb("backtest", "--arms", "ranker");

        Assert.Contains("SND-CLI-001", pending, StringComparison.Ordinal);
        Assert.Contains("pending:API-P6", pending, StringComparison.Ordinal);
        Assert.Equal(1, badCode);
        Assert.Contains(ContextErrors.UnknownArm.Code, bad, StringComparison.Ordinal);
        Assert.Contains("S-8", ranker, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_purge_runs_in_process_and_all_says_the_probe_baselines_are_cleared()
    {
        var (plainCode, plain) = await Verb("purge");
        var (allCode, all) = await Verb("purge", "--all");

        Assert.Equal((0, 0), (plainCode, allCode));
        Assert.DoesNotContain("probe baselines", plain, StringComparison.Ordinal);
        Assert.Contains("the probe baselines are cleared too", all, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_map_with_no_control_plane_is_the_not_running_error()
    {
        var (code, map) = await Verb("map");

        Assert.Equal(2, code);   // the same exit a probe's failed query gets: no server, a tool error
        Assert.Contains("SND-API-001", map, StringComparison.Ordinal);
    }
}
