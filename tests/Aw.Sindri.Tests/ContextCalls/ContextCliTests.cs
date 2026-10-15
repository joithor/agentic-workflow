using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextCliTests
{
    [Fact, Trait("Lane", "unit")]
    public void Query_words_and_flags_make_an_escaped_query_string()
    {
        var parsed = ContextVerbs.QueryArguments.Parse(["split", "pane&x=1", "--depth", "2", "--draft", "--json"]);

        Assert.Equal("?q=split%20pane%26x%3D1&depth=2&draft=true", parsed.Value.QueryString);
        Assert.True(parsed.Value.Json);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("depth", "abc", "(fix: --depth 2)")]
    [InlineData("depth", "9", "(fix: --depth 2)")]
    [InlineData("budget", "abc", "(fix: --budget 4000)")]
    public void The_cli_adds_its_own_flag_to_a_depth_or_budget_006(string field, string text, string fix)
    {
        var error = (field == "depth" ? QueryRules.ParseDepth(text) : QueryRules.ParseBudget(text)).Error;

        var shown = QueryFlagHints.Add(error);

        Assert.Equal(error.Code, shown.Code);
        Assert.StartsWith(error.Message, shown.Message, StringComparison.Ordinal);
        Assert.EndsWith(fix, shown.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Any_other_error_passes_through_the_cli_hints_unchanged()
    {
        var other = ContextErrors.UnknownAnchor.With("no node is named 'x'");

        Assert.Same(other, QueryFlagHints.Add(other));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--colour")]
    [InlineData("--depth")]
    public void An_unknown_flag_or_one_without_its_value_is_SND_CLI_003(string flag) =>
        Assert.Equal("SND-CLI-003", ContextVerbs.QueryArguments.Parse(["x", flag]).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void Text_output_writes_states_as_words_and_keeps_the_fence()
    {
        var at = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var item = new ResultItem("record", Node: null, "Architecture", "<untrusted source=\"repo-docs\">body</untrusted>", 0.016, ["rrf:repo-docs#1"],
            Provenance.Deterministic, Confirmed: false, Trust.Untrusted, ["C1"]);
        var cite = new Citation(RecordKey.From("repo:split-panes/docs/architecture.md"),
            [new CitationRef(SourceId.From("repo-docs"), ExternalId.From("split-panes/docs/architecture.md#L1-L9"), SourceVersion.From("3b18e512dba7"))
                { Current = SourceVersion.From("9c0ffee") }], Permalink: null, Trust.Untrusted, CitationState.Stale);
        var result = new ContextResult(1, QueryId.From("q_1"), new ContextQuery("x"), [], MapVersion: null, at, [new ResultSection("records", [item], ["C2"])], [], [],
            [new DegradedSource(SourceId.From("onyx"), "SND-CONTEXT-010", "Onyx did not answer", at)], Truncated: true, new BudgetUse(10, 9, Estimate: true),
            new Dictionary<string, Citation>(StringComparer.Ordinal) { ["C1"] = cite });

        var text = ContextText.Render(result);

        Assert.Contains("DEGRADED onyx: SND-CONTEXT-010 Onyx did not answer", text, StringComparison.Ordinal);
        Assert.Contains("[C1] Architecture", text, StringComparison.Ordinal);
        Assert.Contains("<untrusted source=\"repo-docs\">body</untrusted>", text, StringComparison.Ordinal);
        Assert.Contains("+1 more (references only): C2", text, StringComparison.Ordinal);
        Assert.Contains("version 3b18e512dba7 → 9c0ffee  stale", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b[", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_affects_item_shows_its_depth_and_path()
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var item = new ResultItem("feature", "feature:tabs.tab-manager", "Tabs", Text: null, Score: null, ["cascade depth 1"], Provenance.Human, Confirmed: true, Trust.Trusted, ["C1"])
        {
            Depth = 1,
            Path = ["feature:layout.split-pane.orientation", "writes thing:pane-layout", "reads action:tab-manager.restore-layout"],
        };
        var result = new ContextResult(1, QueryId.From("q_1"), new ContextQuery(Text: null, Node: "feature:layout.split-pane.orientation"), ["feature:layout.split-pane.orientation"],
            "v1", at, [new ResultSection("affects", [item], [])], [], [], [], Truncated: false, new BudgetUse(10, 9, Estimate: true), new Dictionary<string, Citation>(StringComparer.Ordinal));

        var text = ContextText.Render(result);

        Assert.Contains("anchors: feature:layout.split-pane.orientation · map: v1", text, StringComparison.Ordinal);
        Assert.Contains("depth 1 · path feature:layout.split-pane.orientation → writes thing:pane-layout → reads action:tab-manager.restore-layout", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(text withheld)", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Graph_text_words_every_state_and_its_empty_answers()
    {
        var node = new NodeFact(NodeKey.From("feature:a"), NodeKind.Feature, "A", Provenance.Model, Confirmed: false, Trust.Untrusted);
        var view = new NodeView("v1", node, [], [], []);

        var shown = ContextGraphText.Node(view);
        var lonely = ContextGraphText.Neighbors(new NeighborsView("v1", node.Key, 1, [new NeighborFact(node, 0)], []));
        var nowhere = ContextGraphText.Path(new PathView("v1", node.Key, NodeKey.From("feature:b"), Found: false, []));

        Assert.Contains("model, draft, untrusted", shown, StringComparison.Ordinal);
        Assert.Contains("aliases: none", shown, StringComparison.Ordinal);
        Assert.Contains("no neighbors within 1 edge(s)", lonely, StringComparison.Ordinal);
        Assert.Contains("no path within 8 edges", nowhere, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    public void An_incomplete_probe_run_exits_2_before_it_can_be_a_regression(bool regressed, bool failedProbe, int exit)
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        ProbeScore[] probes = [new ProbeScore(ProbeId.From("one"), 0, 0, [], [], [], failedProbe ? "SND-CONTEXT-011" : null)];
        var report = new ProbeReport(1, "/p/probes.json", "h", "v1", probes, new ProbeTotals(0.6, 1, 1, at), Baseline: null, regressed);

        Assert.Equal(exit, ContextEvalVerbs.Exit(new ProbeRun(report, failedProbe ? null : "/p/r.json")));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_json_of_a_probe_run_is_report_path_incomplete_and_failed_and_the_context_registers_the_type()
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        ProbeScore Score(string id, string? error) => new(ProbeId.From(id), 0, 0, [], [], [], error);
        ProbeReport Report(params ProbeScore[] probes) => new(1, "/p/probes.json", "h", "v1", probes, new ProbeTotals(0, 0, probes.Length, at), Baseline: null, Regressed: false);

        var complete = System.Text.Json.JsonDocument.Parse(Kernel.Json.AwJson.Serialize(new ProbeRun(Report(Score("one", error: null)), "/p/r.json"), SliceHarness.Json)).RootElement;
        var incomplete = System.Text.Json.JsonDocument.Parse(Kernel.Json.AwJson.Serialize(new ProbeRun(Report(Score("one", "SND-CONTEXT-020"), Score("two", "SND-CONTEXT-020")), Path: null),
            SliceHarness.Json)).RootElement;

        Assert.Equal(["report", "path", "incomplete", "failed"], complete.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(("/p/r.json", false, 0), (complete.GetProperty("path").GetString(), complete.GetProperty("incomplete").GetBoolean(), complete.GetProperty("failed").GetArrayLength()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, incomplete.GetProperty("path").ValueKind);
        Assert.True(incomplete.GetProperty("incomplete").GetBoolean());
        Assert.Equal(["one", "two"], incomplete.GetProperty("failed").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.False(incomplete.GetProperty("report").TryGetProperty("incomplete", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_text_of_an_incomplete_run_names_each_failed_probe_and_says_nothing_was_saved()
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var report = new ProbeReport(1, "/p/probes.json", "h", "v1", [new ProbeScore(ProbeId.From("orientation-cascade"), 0, 0, [], [], [], "SND-CONTEXT-011")],
            new ProbeTotals(0, 0, 1, at), Baseline: null, Regressed: false);

        var text = ContextEvalText.Report(report, path: null);

        Assert.Contains("INCOMPLETE", text, StringComparison.Ordinal);
        Assert.Contains("orientation-cascade: SND-CONTEXT-011", text, StringComparison.Ordinal);
        Assert.Contains("not saved", text, StringComparison.Ordinal);
        Assert.DoesNotContain("REGRESSED", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, false, "first run: nothing to compare")]
    [InlineData(0.5, false, "no regression")]
    [InlineData(0.9, true, "REGRESSED: SND-CONTEXT-038")]
    public void Probe_text_prints_the_previous_run_beside_this_one_and_the_verdict_in_words(double? before, bool regressed, string verdict)
    {
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var previous = before is { } b ? new ProbeTotals(b, 1, 1, at.AddDays(-1)) : null;
        var report = new ProbeReport(1, "/p/probes.json", "h", "v1", [new ProbeScore(ProbeId.From("orientation-cascade"), 0.6, 1, [], [], [])],
            new ProbeTotals(0.6, 1, 1, at), previous, regressed);

        var text = ContextEvalText.Report(report, "/home/context/eval/probes/probes-1.json");

        Assert.Contains("orientation-cascade", text, StringComparison.Ordinal);
        Assert.Contains("precision@5 0.60", text, StringComparison.Ordinal);
        Assert.Contains(verdict, text, StringComparison.Ordinal);
    }
}
