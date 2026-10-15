using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Tests.Eval;

public sealed class ProbeRunnerTests
{
    private static readonly ProbeFile _file = new("/probes/probes.json", "abc123",
        new ProbeSet([Answers.Probe("one", nodes: ["feature:a"]), Answers.Probe("two", nodes: ["feature:b"])]));

    private static readonly string[] _nodes = ["feature:a", "feature:b", "feature:c"];
    private static CancellationToken None => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_run_scores_each_probe_and_its_means_with_no_previous()
    {
        var report = (await ProbeRunner.Run(_file, Ask(("one", "feature:a"), ("two", "feature:x")), previous: null, Answers.At, None)).Value;

        Assert.Equal(["one", "two"], report.Probes.Select(p => p.Id.Value), StringComparer.Ordinal);
        Assert.Equal((0.25, 1.0, 2), (report.Totals.PrecisionAt5, report.Totals.CitationCorrectness, report.Totals.Probes));   // one: a + map cite → 1/2; two: 0
        Assert.Equal(("abc123", "v1"), (report.ProbeFileHash, report.MapVersion));
        Assert.Null(report.Previous);
        Assert.False(report.Regressed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0.25, false)]
    [InlineData(0.2, false)]
    [InlineData(0.3, true)]
    public async Task A_run_below_the_previous_one_regressed(double previousPrecision, bool regressed)
    {
        var previous = new ProbeReport(1, _file.Path, _file.Hash, "v1", [], new ProbeTotals(previousPrecision, 1.0, 2, Answers.At.AddDays(-1)), Previous: null, Regressed: false);

        var report = (await ProbeRunner.Run(_file, Ask(("one", "feature:a"), ("two", "feature:x")), previous, Answers.At, None)).Value;

        Assert.Equal(previous.Totals, report.Previous);
        Assert.Equal(regressed, report.Regressed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_question_scores_zero_with_its_code_and_every_one_failing_is_the_error()
    {
        var some = (await ProbeRunner.Run(_file, Fails("two"), previous: null, Answers.At, None)).Value;
        var all = await ProbeRunner.Run(_file, Fails("one", "two"), previous: null, Answers.At, None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, some.Probes[1].Error);
        Assert.Equal(0.0, some.Probes[1].PrecisionAt5);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, all.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_with_a_failed_question_is_incomplete_and_never_a_regression_whatever_its_scores()
    {
        var previous = new ProbeReport(1, _file.Path, _file.Hash, "v1", [], new ProbeTotals(1.0, 1.0, 2, Answers.At.AddDays(-1)), Previous: null, Regressed: false);

        var report = (await ProbeRunner.Run(_file, Fails("two"), previous, Answers.At, None)).Value;

        Assert.True(report.Incomplete);
        Assert.False(report.Regressed);
        Assert.False(report.IsBaseline);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 1, false)]
    public async Task A_run_that_names_more_absent_nodes_than_the_previous_one_regressed_even_when_the_precision_holds(int before, int now, bool regressed)
    {
        var file = new ProbeFile("/probes/probes.json", "abc123", new ProbeSet([Answers.Probe("one", nodes: ["feature:a"], absent: ["feature:b", "feature:c"])]));
        var previous = new ProbeReport(1, file.Path, file.Hash, "v1", [], new ProbeTotals(0.0, 1.0, 1, Answers.At.AddDays(-1), AbsentReturned: before), Previous: null, Regressed: false);
        var items = _nodes.Take(1 + now).Select(n => ("graph", Answers.Item(n))).ToArray();

        var report = (await ProbeRunner.Run(file, (_, _) => ValueTask.FromResult(Results.Success(Answers.Of(items))), previous, Answers.At, None)).Value;

        Assert.Equal(now, report.Totals.AbsentReturned);
        Assert.Equal(regressed, report.Regressed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"probes":[]}""", "probes")]
    [InlineData("""{"schemaVersion":2,"probes":[{"id":"a","question":"q","expect":{}}]}""", "schemaVersion")]
    [InlineData("""{"probes":[{"id":"a","question":"q","expect":{}},{"id":"a","question":"q","expect":{}}]}""", "probes[1].id")]
    [InlineData("""{"probes":[{"id":"Not A Slug","question":"q","expect":{}}]}""", "probes[0].id")]
    [InlineData("""{"probes":[{"id":"a","question":" ","expect":{}}]}""", "probes[0].question")]
    public void A_bad_probe_file_is_SND_CONTEXT_035_at_its_key_path(string json, string keyPath)
    {
        var set = Kernel.Json.AwJson.Deserialize<ProbeSet>(System.Text.Encoding.UTF8.GetBytes(json), EvalJson.Options).Value;

        var error = ProbeRunner.Check(set);

        Assert.Equal(ContextErrors.ProbeFileInvalid.Code, error?.Code);
        Assert.StartsWith(keyPath + ":", error!.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_fixture_probe_file_is_valid()
    {
        var set = Kernel.Json.AwJson.Deserialize<ProbeSet>(System.Text.Encoding.UTF8.GetBytes(Testing.Fixtures.SplitPanesFixture.ReadText("probes.json")), EvalJson.Options);

        Assert.True(set.IsSuccess, set.IsError ? set.Error.Message : null);
        Assert.Null(ProbeRunner.Check(set.Value));
        Assert.Equal(8, set.Value.Probes.Count);
    }

    /// <summary>Answers each probe with one graph item naming the given node.</summary>
    private static Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> Ask(params (string Id, string Node)[] answers) =>
        (probe, _) => ValueTask.FromResult(Results.Success(Answers.Of(("graph", Answers.Item(answers.Single(a => a.Id == probe.Id.Value).Node)))));

    /// <summary>Answers SND-CONTEXT-020 for the given probes and an empty answer for the rest.</summary>
    private static Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> Fails(params string[] ids) =>
        (probe, _) => ValueTask.FromResult(ids.Contains(probe.Id.Value) ? (Result<ContextResult>)ContextErrors.SessionsDisabled.With("off") : Answers.Of());
}
