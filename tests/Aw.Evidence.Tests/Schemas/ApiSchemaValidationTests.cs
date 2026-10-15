using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Tests.ApiRun;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>A summary api-run produced, and a script it accepts, validate against the committed schemas; a script it refuses does not.</summary>
public sealed class ApiSchemaValidationTests
{
    private const string Notes = ApiRunHarness.Origin + "/api/notes";
    private const string Get = """{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200,"pointers":[{"pointer":"/n","equals":1}]}}]}""";

    private static ApiRunHarness With(string script)
    {
        var harness = new ApiRunHarness().WithScript(script);
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, """{"n":1}"""));
        return harness;
    }

    private static IReadOnlyList<string> Violations(string schemaName, string document) =>
        MiniSchema.Violations(EvidenceSchemaTests.Committed(schemaName), JsonNode.Parse(document));

    [Fact, Trait("Lane", "integration")]
    public async Task A_passed_summary_validates_against_the_schema()
    {
        var response = (await With(Get).Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Empty(Violations("api-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_failed_summary_with_its_failures_validates_against_the_schema()
    {
        var response = (await With(Get.Replace("\"equals\":1", "\"equals\":2", StringComparison.Ordinal)).Run()).Value;

        Assert.NotEmpty(response.Summary.Failures);
        Assert.Empty(Violations("api-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refused_summary_validates_against_the_schema()
    {
        var response = (await With(Get).Run(ApiRunHarness.Request(host: null))).Value;

        Assert.NotNull(response.Summary.Refusal);
        Assert.Empty(Violations("api-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_errored_summary_validates_against_the_schema()
    {
        var harness = With(Get);
        _ = harness.Transport.Unreachable(Notes);

        var response = (await harness.Run()).Value;

        Assert.NotNull(response.Summary.Error);
        Assert.Empty(Violations("api-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_schema_rejects_a_summary_that_lacks_a_key_or_names_an_unknown_outcome()
    {
        var document = """{"schema":"evidence.api.summary/1","outcome":"maybe"}""";

        var violations = Violations("api-summary", document);

        Assert.Contains(violations, static v => v.Contains("outcome", StringComparison.Ordinal));
        Assert.Contains(violations, static v => v.Contains("missing required property", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_valid_script_validates_and_a_script_with_a_literal_header_or_an_unknown_key_does_not()
    {
        const string valid = """{"requests":[{"method":"POST","path":"/a","headers":{"X-Key":{"env":"NOTES_KEY"}},"body":{"a":1},"expect":{"status":201}}],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}],"declaredWrites":["add"],"passEnv":["LANG"]}""";
        const string literal = """{"requests":[{"method":"GET","path":"/a","headers":{"X-Key":"abc"},"expect":{"status":200}}]}""";
        const string unknown = """{"requests":[{"method":"GET","path":"/a","expect":{"status":200,"statuss":1}}]}""";

        Assert.Empty(Violations("api-script", valid));
        Assert.NotEmpty(Violations("api-script", literal));
        Assert.NotEmpty(Violations("api-script", unknown));
    }
}
