using Aw.Context.Core.Engine;
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
}
