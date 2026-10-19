using System.Net;
using Aw.Context.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri context subgraph': how the arguments become a GET or a QUERY (scoping spec C14).</summary>
public sealed class ContextSubgraphCliTests
{
    private static ContextSubgraphArguments Parse(string[] args, string stdin = "") => ContextSubgraphArguments.Parse(args, new StringReader(stdin)).Value;

    private static string[] Keys(int count) => [.. Enumerable.Range(0, count).Select(i => "feature:k." + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];

    [Fact, Trait("Lane", "unit")]
    public void Cli_subgraph_switches_to_a_body_over_20_keys_or_6000_chars()
    {
        var twenty = Parse(Keys(20));
        var twentyOne = Parse(Keys(21));
        var long_ = Parse(["feature:" + new string('a', 3000), "feature:" + new string('b', 3000)]);

        Assert.False(twenty.UseBody);
        Assert.StartsWith("?nodes=feature%3Ak.0&nodes=", twenty.QueryString, StringComparison.Ordinal);
        Assert.True(twentyOne.UseBody);
        Assert.True(long_.UseBody);
    }

    [Fact, Trait("Lane", "integration")]   // writes a temp file
    public void Cli_subgraph_keys_from_stdin_and_a_file_skip_blank_lines_and_comments_and_join_the_words()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "# the packet\nfeature:a.b\n\n  code:src/x.ts  \n");

            var fromFile = Parse(["--keys-from", file, "feature:c.d", "--depth", "2", "--map-version", "v3", "--json"]);
            var fromStdin = Parse(["--keys-from", "-"], "feature:s.t\n# no\nthing:u\n");

            Assert.Equal(["feature:c.d", "feature:a.b", "code:src/x.ts"], fromFile.Keys);
            Assert.Equal(("2", "v3", true), (fromFile.Depth, fromFile.MapVersion, fromFile.Json));
            Assert.Equal(["feature:s.t", "thing:u"], fromStdin.Keys);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Cli_subgraph_with_no_keys_an_unknown_flag_or_an_unreadable_file_is_006()
    {
        var none = ContextSubgraphArguments.Parse([], new StringReader(string.Empty));
        var flag = ContextSubgraphArguments.Parse(["feature:a.b", "--nope"], new StringReader(string.Empty));
        var missing = ContextSubgraphArguments.Parse(["--keys-from", "/no/such/file"], new StringReader(string.Empty));

        Assert.All([none, flag, missing], r => Assert.Equal(ContextErrors.QueryInvalid.Code, r.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_subgraph_sends_a_GET_for_20_keys_and_an_HTTP_QUERY_with_a_JSON_body_for_21()
    {
        var empty = """{"nodes":[],"missing":[]}""";
        var probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
        var api = new ScriptedApi((HttpStatusCode.OK, empty), (HttpStatusCode.OK, empty));
        using var client = new ControlPlaneClient(probe, SliceHarness.Json, handler: api);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var verbs = new ContextSubgraphVerbs(client, new Output(output, error, SliceHarness.Json), new StringReader(string.Empty));

        _ = await verbs.Run([.. Keys(20), "--json"], TestContext.Current.CancellationToken);
        _ = await verbs.Run([.. Keys(21), "--depth", "2", "--json"], TestContext.Current.CancellationToken);

        Assert.Equal(("GET", true), (api.Calls[0].Method, api.Calls[0].Route.StartsWith("/sindri/context/subgraph?nodes=", StringComparison.Ordinal)));
        Assert.Equal(("QUERY", "/sindri/context/subgraph"), (api.Calls[1].Method, api.Calls[1].Route));
        Assert.Contains("\"depth\":2", api.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("feature:k.20", api.Calls[1].Body, StringComparison.Ordinal);
    }
}
