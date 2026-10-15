using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.ApiRun;

public sealed class ApiRunCliTests
{
    private const string Tool = "/work/repo/out/tool";

    private static ApiRunHarness With(string script)
    {
        var harness = new ApiRunHarness().WithScript(script);
        harness.Targets.Targets = new ResolvedTargets(["out"], [], []);
        _ = harness.Locator.Directory("/work/repo/out").Executable(Tool);
        _ = harness.Cli.Program(Tool, new CliOutcome(0, """{"count":2}""", string.Empty));
        return harness;
    }

    private static string Script(string argv, string expect = """{"exitCode":0}""", string extra = "") =>
        """{EXTRA"cli":[{"argv":ARGV,"expect":EXPECT}]}""".Replace("EXTRA", extra, StringComparison.Ordinal).Replace("ARGV", argv, StringComparison.Ordinal).Replace("EXPECT", expect, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_runs_a_cli_target_inside_a_build_output_and_checks_its_stdout()
    {
        var harness = With(Script("""["out/tool","list"]""", """{"exitCode":0,"pointers":[{"pointer":"/count","equals":2}]}"""));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        var step = Assert.Single(response.Summary.Steps);
        Assert.Equal((1, ApiAction.Cli, "out/tool list", ApiStepStatus.Passed, 0), (step.Index, step.Action, step.Target, step.Status, step.Code));
        var call = Assert.Single(harness.Cli.Calls);
        Assert.Equal(Tool, call.Executable);
        Assert.Equal(["list"], call.Args);
        Assert.Null(response.Summary.Host);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_reports_a_wrong_exit_code_as_a_failure()
    {
        var harness = With(Script("""["out/tool"]""", """{"exitCode":1}"""));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(ApiOutcome.Failed, response.Summary.Outcome);
        Assert.Equal("out/tool: exit code 0, expected 1 (assertion 1 of 1)", response.Summary.Headline);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_runs_the_requests_first_then_the_cli_calls_with_continuing_indexes()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}}],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        _ = harness.Transport.Reply("GET", ApiRunHarness.Origin + "/api/notes", new HttpReply(200, Location: null, "{}"));

        var response = (await harness.Run()).Value;

        Assert.Equal([(1, ApiAction.Request), (2, ApiAction.Cli)], response.Summary.Steps.Select(static s => (s.Index, s.Action)).ToList());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_cli_target_outside_build_outputs_and_the_allowlist()
    {
        var harness = With(Script("""["/usr/bin/other"]"""));
        _ = harness.Locator.Executable("/usr/bin/other");

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(ApiOutcome.Refused, response.Summary.Outcome);
        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Cli.Calls);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_never_resolves_a_cli_target_through_PATH()
    {
        var harness = With(Script("""["tool"]"""));
        _ = harness.Locator.Executable("/usr/bin/tool");

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Locator.Queries);
        Assert.Empty(harness.Cli.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_symlink_that_leaves_the_build_output()
    {
        var harness = With(Script("""["out/tool"]"""));
        _ = harness.Locator.Executable(Tool, "/usr/bin/other");

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Cli.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_with_no_targets_config_refuses_every_cli_call_naming_the_file_to_write()
    {
        var harness = With(Script("""["out/tool"]"""));
        harness.Targets.Targets = ResolvedTargets.Empty;

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Contains(".agentic-workflow/evidence.json", response.Summary.Refusal?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_loads_the_targets_for_the_repo_and_the_config_override()
    {
        var harness = With(Script("""["out/tool"]"""));

        _ = await harness.Run(ApiRunHarness.Request(host: null) with { ConfigPath = "/work/targets.json" });

        Assert.Equal([(ApiRunHarness.Repo, "/work/targets.json")], harness.Targets.Loads.ToList());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_write_verb_the_script_did_not_declare()
    {
        var harness = With(Script("""["out/tool","delete","7"]"""));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
        Assert.Contains("'delete'", response.Summary.Refusal?.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Cli.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_shows_declared_writes_in_the_summary_and_runs_them()
    {
        var harness = With(Script("""["out/tool","delete","7"]""", extra: """ "declaredWrites":["delete"],"""));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Equal(["delete"], response.Summary.DeclaredWrites);
        _ = Assert.Single(harness.Cli.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"passEnv":["GH_TOKEN"],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""")]
    [InlineData("""{"passEnv":["DATABASE_URL"],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""")]
    public async Task ApiRun_refuses_to_pass_through_a_secret_named_variable(string script)
    {
        var harness = With(script);

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Cli.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_secret_named_variable_from_the_targets_config_too()
    {
        var harness = With(Script("""["out/tool"]"""));
        harness.Targets.Targets = new ResolvedTargets(["out"], [], ["AWS_PROFILE"]);

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.CliRefused.Code, response.Summary.Refusal?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_gives_a_cli_call_only_LANG_and_the_declared_variables_and_records_their_names()
    {
        var harness = With(Script("""["out/tool"]""", extra: """ "passEnv":["NOTES_REGION","NOTES_UNSET"],"""));
        harness.Targets.Targets = new ResolvedTargets(["out"], [], ["TZ"]);
        harness.Environment["NOTES_REGION"] = "north-9";
        harness.Environment["TZ"] = "UTC";
        harness.Environment["UNRELATED"] = "not passed";

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        var environment = Assert.Single(harness.Cli.Calls).Environment;
        Assert.Equal(["LANG", "NOTES_REGION", "TZ"], environment.Keys.Order(StringComparer.Ordinal).ToList());
        Assert.Equal("north-9", environment["NOTES_REGION"]);
        Assert.Equal(["NOTES_REGION", "TZ"], response.Summary.PassedEnv);
        Assert.DoesNotContain("north-9", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_sends_stdin_to_the_program()
    {
        var harness = new ApiRunHarness().WithScript("""{"cli":[{"argv":["out/tool"],"stdin":"hello","expect":{"exitCode":0}}]}""");
        harness.Targets.Targets = new ResolvedTargets(["out"], [], []);
        _ = harness.Locator.Directory("/work/repo/out").Executable(Tool);

        _ = await harness.Run(ApiRunHarness.Request(host: null));

        Assert.Equal("hello", Assert.Single(harness.Cli.Calls).Stdin);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_marks_a_program_that_will_not_start_broken_and_skips_the_rest()
    {
        var harness = With("""{"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}},{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        _ = harness.Cli.WontStart.Add(Tool);

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(ApiOutcome.Errored, response.Summary.Outcome);
        Assert.Equal([ApiStepStatus.Broken, ApiStepStatus.Skipped], response.Summary.Steps.Select(static s => s.Status).ToList());
        Assert.Equal(EvidenceErrors.RunFailed.Code, response.Summary.Error?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_skips_the_cli_calls_after_a_request_that_failed()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}}],"cli":[{"argv":["out/tool"],"expect":{"exitCode":0}}]}""");
        _ = harness.Transport.Unreachable(ApiRunHarness.Origin + "/api/notes");

        var response = (await harness.Run()).Value;

        Assert.Equal([ApiStepStatus.Broken, ApiStepStatus.Skipped], response.Summary.Steps.Select(static s => s.Status).ToList());
        Assert.Empty(harness.Cli.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_redacts_cli_stdout()
    {
        var harness = With(Script("""["out/tool"]"""));
        _ = harness.Scrubber.Hides("tok-123");
        _ = harness.Cli.Program(Tool, new CliOutcome(0, "found tok-123", string.Empty));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.DoesNotContain("tok-123", response.Document, StringComparison.Ordinal);
        Assert.Equal("found [REDACTED]", response.Summary.Steps[0].Output);
    }
}
