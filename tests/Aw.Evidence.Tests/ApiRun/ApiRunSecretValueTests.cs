using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.ApiRun;

/// <summary>A header or pass-through value the run resolved is scrubbed by value from everything it records, wherever the app echoes it.</summary>
public sealed class ApiRunSecretValueTests
{
    private const string Key = "opaque-key-9f8c2d41";
    private const string Notes = ApiRunHarness.Origin + "/api/notes";
    private const string Script = """{"requests":[{"method":"GET","path":"/api/notes","headers":{"X-Api-Key":{"env":"API_KEY"}},"expect":{"status":200,"pointers":[{"pointer":"/debug/echo","equals":"fine"}]}}]}""";

    private static ApiRunHarness With(string body)
    {
        var harness = new ApiRunHarness().WithScript(Script);
        harness.Environment["API_KEY"] = Key;
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(401, Location: null, body));
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_a_header_value_the_app_echoes_under_a_member_that_is_not_secret_named()
    {
        var harness = With("""{"error":"invalid key KEY","debug":{"echo":"KEY"}}""".Replace("KEY", Key, StringComparison.Ordinal));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain(Key, response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, response.Summary.Headline, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:header-value]", response.Document, StringComparison.Ordinal);
        Assert.NotEmpty(response.Summary.Failures);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_a_header_value_in_a_plain_text_body()
    {
        var harness = With($"invalid key {Key}, go away");

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain(Key, response.Document, StringComparison.Ordinal);
        Assert.StartsWith("invalid key [REDACTED:header-value]", response.Summary.Steps[0].Output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_leaves_a_short_value_alone_so_it_does_not_mangle_ordinary_text()
    {
        var harness = With("a short value ok");
        harness.Environment["API_KEY"] = "short";
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(401, Location: null, "this is short text"));

        var response = (await harness.Run()).Value;

        Assert.Equal("this is short text", response.Summary.Steps[0].Output);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_a_passed_through_variable_value_a_program_prints()
    {
        const string Tool = "/work/repo/out/tool";
        var harness = new ApiRunHarness().WithScript("""{"passEnv":["NOTES_REGION"],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        harness.Targets.Targets = new ResolvedTargets(["out"], [], []);
        _ = harness.Locator.Directory("/work/repo/out").Executable(Tool);
        harness.Environment["NOTES_REGION"] = "north-region-77";
        _ = harness.Cli.Program(Tool, invocation => new CliOutcome(0, "region=" + invocation.Environment["NOTES_REGION"], string.Empty));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.DoesNotContain("north-region-77", response.Document, StringComparison.Ordinal);
        Assert.Equal("region=[REDACTED:env-value]", response.Summary.Steps[0].Output);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_the_token_of_a_scheme_credential_header_the_app_echoes_alone()
    {
        const string Token = "tok-9f8c2d41ab7e";
        var harness = new ApiRunHarness().WithScript("""{"requests":[{"method":"GET","path":"/api/notes","headers":{"Authorization":{"env":"AUTH"}},"expect":{"status":200}}]}""");
        harness.Environment["AUTH"] = "Bearer " + Token;
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(401, Location: null, $$"""{"error":"bad token {{Token}}"}"""));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain(Token, response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, response.Summary.Headline, StringComparison.Ordinal);
        Assert.NotEmpty(response.Summary.Failures);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_a_value_that_json_escapes_and_leaves_one_below_the_minimum_length()
    {
        const string Tricky = "ab+\"cd\\ef==";
        var harness = With("""{"echo":"ab+\"cd\\ef==","short":"abcde"}""");
        harness.Environment["API_KEY"] = Tricky;

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("ef==", response.Document, StringComparison.Ordinal);
        Assert.Contains("\"short\":\"abcde\"", response.Summary.Steps[0].Output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_records_the_host_intact_when_a_passed_through_value_equals_it()
    {
        var harness = new ApiRunHarness().WithScript("""{"passEnv":["NOTES_REGION"],"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}}],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        harness.Targets.Targets = new ResolvedTargets(["out"], [], []);
        _ = harness.Locator.Directory("/work/repo/out").Executable("/work/repo/out/tool");
        harness.Environment["NOTES_REGION"] = ApiRunHarness.Origin;
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "{}"));
        _ = harness.Cli.Program("/work/repo/out/tool", _ => new CliOutcome(0, "ok", string.Empty));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiRunHarness.Origin, response.Summary.Host);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_keeps_the_summary_valid_when_a_passed_through_value_equals_an_enum_name()
    {
        var harness = new ApiRunHarness().WithScript("""{"passEnv":["NOTES_REGION"],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        harness.Targets.Targets = new ResolvedTargets(["out"], [], []);
        _ = harness.Locator.Directory("/work/repo/out").Executable("/work/repo/out/tool");
        harness.Environment["NOTES_REGION"] = "passed";
        _ = harness.Cli.Program("/work/repo/out/tool", _ => new CliOutcome(0, "ok", string.Empty));

        var result = await harness.Run(ApiRunHarness.Request(host: null));

        Assert.False(result.IsError);
        Assert.Equal(ApiOutcome.Passed, result.Value.Summary.Outcome);
        Assert.Equal(0, result.Value.Summary.ExitCode());
    }
}
