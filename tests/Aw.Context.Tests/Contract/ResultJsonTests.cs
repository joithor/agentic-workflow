using System.Text;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Contract;

public sealed class ResultJsonTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ContractJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void A_result_round_trips_and_writes_the_spec_names()
    {
        var at = new DateTimeOffset(2026, 10, 10, 18, 2, 11, TimeSpan.Zero);
        var cite = new Citation(RecordKey.From("repo:split-panes/docs/architecture.md"),
            [new CitationRef(SourceId.From("repo-docs"), ExternalId.From("split-panes/docs/architecture.md#L1-L9"), SourceVersion.From("3b18e51"))
                { Current = SourceVersion.From("9c0ffee") }],
            Permalink: null, Trust.Untrusted, CitationState.Stale);
        var item = new ResultItem("record", Node: null, "Architecture", "<untrusted source=\"repo-docs\">text</untrusted>", 0.016,
            ["rrf:repo-docs#1"], Provenance.Deterministic, Confirmed: false, Trust.Untrusted, ["C1"])
        { Tokens = 12 };
        var result = new ContextResult(ContextResult.CurrentSchemaVersion, QueryId.From("q_1"), new ContextQuery("split pane"), [], MapVersion: null, at,
            [new ResultSection("records", [item], [])], [], [],
            [new DegradedSource(SourceId.From("onyx"), "SND-CONTEXT-010", "Onyx did not answer", at)], Truncated: false,
            new BudgetUse(6000, 12, Estimate: true), new Dictionary<string, Citation>(StringComparer.Ordinal) { ["C1"] = cite });

        var bytes = AwJson.Serialize(result, _json);
        var text = Encoding.UTF8.GetString(bytes);
        var back = AwJson.Deserialize<ContextResult>(bytes, _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(text, Encoding.UTF8.GetString(AwJson.Serialize(back.Value, _json)));
        Assert.Contains("\"schemaVersion\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"stale\"", text, StringComparison.Ordinal);
        Assert.Contains("\"trust\":\"untrusted\"", text, StringComparison.Ordinal);
        Assert.Contains("\"provenance\":\"deterministic\"", text, StringComparison.Ordinal);
        Assert.Contains("\"current\":\"9c0ffee\"", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Scrub_blocked_is_written_in_kebab_case() =>
        Assert.Equal("\"scrub-blocked\"", Encoding.UTF8.GetString(AwJson.Serialize(CitationState.ScrubBlocked, _json)));
}
