using System.Net;
using Aw.Context.Core.Code;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri context code' over a scripted API: what it sends, what it prints and the exit codes (0 ok, 1 attention, 2 usage).</summary>
public sealed class ContextCodeVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (HttpStatusCode, string) Problem(HttpStatusCode status, string code, string detail) =>
        (status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"type":"x","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        return await new ContextCodeVerbs(client, new Output(_out, _error, SliceHarness.Json)).Run(args, Token);
    }

    internal static CodeView View()
    {
        var key = NodeKey.From("feature:sharing");
        var at = DateTimeOffset.Parse("2026-10-16T18:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var searched = new LensSearched(
            ["feature:sharing"], "v2", 0, [],
            [
                SearchedSources.Lexical(Context.Core.Sources.SourceId.From("lexical-tests"), SearchedState.Unavailable, "commit-not-held", "SND-CONTEXT-110", asOf: null),
                SearchedSources.Lexical(Context.Core.Sources.SourceId.From("lexical-schema"), SearchedState.Degraded, reason: null, "SND-CONTEXT-152", at),
            ],
            new SearchedBands(Intent: new BandSearched(Empty: true), Product: new BandSearched(Empty: false), Code: new BandSearched(Empty: false)));
        var coverage = new LensCoverage(Scope: 1, new CoverageIntent(Covered: 0, Of: 1), new CoverageProduct(Confirmed: 1, Of: 1), new CoverageCode(Implemented: 1, Of: 1, Level: "file", Suggested: 0), []);
        return new CodeView(
            1, [new LensAnchor(key, "Sharing", NodeKind.Feature, Trust.Trusted)], ["feature:nope"], "v2", Draft: false, at,
            [new CodeFile("src/sharing/links.ts", "src/sharing/links.ts", NodeKind.Code, [key], [], Provenance.Human, 1.0, Confirmed: true, Suggested: false, LinkId: null, Trust.Trusted, [key], ["tests/links.test.ts"])],
            [new CodeTest("tests/links.test.ts", "tests/links.test.ts", NodeKind.Code, CodeBasis.Imports, [], ["src/sharing/links.ts"], Confirmed: false, Suggested: true, LinkId: null, Trust.Trusted, [key])],
            new CodeData([], []), CodeSymbols.Unavailable, CodeSymbols.Unavailable,
            new CodeLensCounts(Files: 1, Tests: 1, Things: 0, SchemaFiles: 0, FilesWithoutTest: 0, Unlocated: 0), new CodeLensTruncated(Files: false, Tests: false, Things: false, SchemaFiles: false, SearchedFiles: false), [], [], coverage, searched,
            ["lexical-schema: SND-CONTEXT-152"], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Code_sends_every_anchor_the_draft_flag_and_the_map_version_escaped()
    {
        var api = new ScriptedApi(Problem(HttpStatusCode.NotFound, "SND-CONTEXT-120", "map version v99 isn't held"));

        var exit = await Run(api, "feature:a", "feature:b", "--map-version", "v99");

        Assert.Equal("/sindri/context/code?anchor=feature%3Aa&anchor=feature%3Ab&mapVersion=v99", api.Calls[0].Route);
        Assert.Equal(1, exit);   // 120 is attention
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Code_draft_with_a_map_version_is_006_and_sends_nothing()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, "{}"));

        var exit = await Run(api, "feature:a", "--draft", "--map-version", "v1");

        Assert.Equal(1, exit);
        Assert.Empty(api.Calls);
        Assert.Contains("SND-CONTEXT-006", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--json")]
    [InlineData("--map-version")]
    [InlineData("--nope")]
    public async Task Code_without_an_anchor_or_with_a_bad_flag_is_a_usage_error_sent_nowhere(string flag)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, "{}"));

        var exit = await Run(api, flag);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Code_prints_files_tests_the_unavailable_symbols_and_each_searched_source_as_a_word()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json)));

        var exit = await Run(api, "feature:sharing");

        var text = _out.ToString();
        Assert.True(exit == 0, _error.ToString());
        Assert.Contains("src/sharing/links.ts  implements feature:sharing  confirmed  tested by tests/links.test.ts", text, StringComparison.Ordinal);
        Assert.Contains("tests/links.test.ts  imports  suggested  names src/sharing/links.ts", text, StringComparison.Ordinal);
        Assert.Contains("callers unavailable (index-not-built) · callees unavailable (index-not-built)", text, StringComparison.Ordinal);
        Assert.Contains("lexical-tests unavailable (SND-CONTEXT-110)", text, StringComparison.Ordinal);
        Assert.Contains("lexical-schema degraded (SND-CONTEXT-152)", text, StringComparison.Ordinal);
        Assert.Contains("degraded: lexical-schema: SND-CONTEXT-152", text, StringComparison.Ordinal);
        Assert.Contains("missing anchor: feature:nope", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Code_json_prints_the_body_as_served()
    {
        var body = System.Text.Json.JsonSerializer.Serialize(View(), SliceHarness.Json);
        var api = new ScriptedApi((HttpStatusCode.OK, body));

        var exit = await Run(api, "feature:sharing", "--json");

        Assert.True(exit == 0, _error.ToString());
        Assert.Contains("\"schemaVersion\":1", _out.ToString(), StringComparison.Ordinal);
    }
}
