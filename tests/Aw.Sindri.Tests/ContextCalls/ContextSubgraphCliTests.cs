using Aw.Context.Core;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri context subgraph': how the arguments become a GET or a POST (scoping spec C14).</summary>
public sealed class ContextSubgraphCliTests
{
    private static ContextSubgraphArguments Parse(string[] args, string stdin = "") => ContextSubgraphArguments.Parse(args, new StringReader(stdin)).Value;

    private static string[] Keys(int count) => [.. Enumerable.Range(0, count).Select(i => "feature:k." + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];

    [Fact, Trait("Lane", "unit")]
    public void Cli_subgraph_switches_to_post_over_20_keys_or_6000_chars()
    {
        var twenty = Parse(Keys(20));
        var twentyOne = Parse(Keys(21));
        var long_ = Parse(["feature:" + new string('a', 3000), "feature:" + new string('b', 3000)]);

        Assert.False(twenty.UsePost);
        Assert.StartsWith("?nodes=feature%3Ak.0&nodes=", twenty.QueryString, StringComparison.Ordinal);
        Assert.True(twentyOne.UsePost);
        Assert.True(long_.UsePost);
    }

    [Fact, Trait("Lane", "unit")]
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
}
