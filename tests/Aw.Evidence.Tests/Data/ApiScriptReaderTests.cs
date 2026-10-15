using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class ApiScriptReaderTests
{
    private const string Valid =
        """{"schema":"evidence.api.run/1","declaredWrites":["add"],"passEnv":["LANG"],"requests":[""" +
        """{"method":"POST","path":"/api/notes","headers":{"Authorization":{"env":"NOTES_TOKEN"}},"body":{"title":"hello"},"expect":""" +
        """{"status":201,"pointers":[{"pointer":"/title","equals":"hello"},{"pointer":"/id","exists":true},{"pointer":"/created","matches":"^20"}]}},""" +
        """{"method":"GET","path":"/api/notes","expect":{"status":200,"golden":"notes.json"}}],""" +
        """ "cli":[{"argv":["out/notes","list"],"stdin":"x","expect":{"exitCode":0}}]}""";

    private static Kernel.Result<ApiScript> Read(string json) => ApiScriptReader.Read(TestJson.Utf8(json), TestJson.Options);

    private static string OneRequest(string request) => $$"""{"requests":[{{request}}]}""";

    [Fact, Trait("Lane", "unit")]
    public void ApiScript_reads_requests_cli_calls_and_assertions_into_typed_steps()
    {
        var script = Read(Valid).Value;

        Assert.Equal(2, script.RequestList.Count);
        Assert.Equal(HttpVerb.Post, script.RequestList[0].Method);
        Assert.Equal("NOTES_TOKEN", script.RequestList[0].Headers!["Authorization"].Env);
        Assert.Equal("hello", script.RequestList[0].Expect.Pointers![0].EqualTo.GetString());
        Assert.True(script.RequestList[0].Expect.Pointers![1].Exists);
        Assert.Equal("^20", script.RequestList[0].Expect.Pointers![2].Matches);
        Assert.Equal("notes.json", script.RequestList[1].Expect.Golden);
        Assert.Equal(["out/notes", "list"], script.CliList[0].Argv);
        Assert.Equal(["add"], script.DeclaredWrites);
    }

    [Fact, Trait("Lane", "unit")]
    public void ApiScript_tells_an_equals_null_from_no_equals()
    {
        var script = Read(OneRequest("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointer":"/a","equals":null}]}}""")).Value;

        Assert.Equal(JsonValueKind.Null, script.RequestList[0].Expect.Pointers![0].EqualTo.ValueKind);
    }

    [Fact, Trait("Lane", "unit")]
    public void ApiScript_refuses_an_unknown_key_naming_its_json_location()
    {
        var result = Read(OneRequest("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointr":"/a","exists":true}]}}"""));

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains("$.requests[0].expect.pointers[0]", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown property", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void ApiScript_refuses_a_literal_header_value()
    {
        var result = Read(OneRequest("""{"method":"GET","path":"/a","headers":{"Authorization":"Bearer abc"},"expect":{"status":200}}"""));

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains("$.requests[0].headers.Authorization", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("never a literal value", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer abc", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://other.example/x")]
    [InlineData("//other.example/x")]
    [InlineData("api/notes")]
    [InlineData("/a b")]
    [InlineData("/a#frag")]
    [InlineData("/a\\b")]
    [InlineData("")]
    public void ApiScript_refuses_a_path_that_leaves_the_host(string path)
    {
        var result = Read(OneRequest("""{"method":"GET","path":"PATH","expect":{"status":200}}""".Replace("PATH", path.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal)));

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains("requests[0]: path", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("a/b")]
    [InlineData("/a~2b")]
    [InlineData("#/a")]
    public void ApiScript_refuses_an_unknown_pointer_syntax(string reference)
    {
        var result = Read(OneRequest("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointer":"PTR","exists":true}]}}""".Replace("PTR", reference, StringComparison.Ordinal)));

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains("pointers[0]", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"method":"GET","path":"/a","body":{"x":1},"expect":{"status":200}}""", "body")]
    [InlineData("""{"method":"GET","path":"/a","expect":{}}""", "status")]
    [InlineData("""{"method":"GET","path":"/a","expect":{"status":99}}""", "status")]
    [InlineData("""{"method":"GET","path":"/a","expect":{"status":200,"exitCode":0}}""", "exitCode")]
    [InlineData("""{"method":"GET","path":"/a","headers":{"X-Key":{"env":"lower"}},"expect":{"status":200}}""", "UPPER_SNAKE")]
    [InlineData("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointer":"/a"}]}}""", "exactly one of equals, exists and matches")]
    [InlineData("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointer":"/a","exists":true,"matches":"x"}]}}""", "exactly one of equals, exists and matches")]
    [InlineData("""{"method":"GET","path":"/a","expect":{"status":200,"pointers":[{"pointer":"/a","matches":"("}]}}""", "regular expression")]
    public void ApiScript_refuses_a_request_that_breaks_a_rule(string request, string rule)
    {
        var result = Read(OneRequest(request));

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains(rule, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"requests":[]}""", "at least one")]
    [InlineData("""{"schema":"other/1","requests":[{"method":"GET","path":"/a","expect":{"status":200}}]}""", "evidence.api.run/1")]
    [InlineData("""{"cli":[{"argv":[],"expect":{"exitCode":0}}]}""", "argv")]
    [InlineData("""{"cli":[{"argv":["out/x"],"expect":{}}]}""", "exitCode")]
    [InlineData("""{"cli":[{"argv":["out/x"],"expect":{"exitCode":256}}]}""", "exitCode")]
    [InlineData("""{"cli":[{"argv":["out/x"],"expect":{"exitCode":0,"status":200}}]}""", "status")]
    [InlineData("""{"passEnv":["lower"],"cli":[{"argv":["out/x"],"expect":{"exitCode":0}}]}""", "passEnv")]
    public void ApiScript_refuses_a_script_that_breaks_a_rule(string json, string rule)
    {
        var result = Read(json);

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.Contains(rule, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void ApiScript_reports_every_problem_at_once()
    {
        var result = Read(OneRequest("""{"method":"GET","path":"a","body":1,"expect":{}}"""));

        Assert.Contains("path", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("body", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("status", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"requests":[{"method":"get","path":"/a","expect":{"status":200}}]}""")]
    [InlineData("""{"requests":[{"method":"GET","expect":{"status":200}}]}""")]
    [InlineData("not json")]
    public void ApiScript_refuses_malformed_input_with_a_location(string json)
    {
        var result = Read(json);

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, result.Error.Code);
        Assert.StartsWith("$", result.Error.Message, StringComparison.Ordinal);
    }
}
