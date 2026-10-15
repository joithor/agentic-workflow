using System.Text;
using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.ApiRun;

public sealed class ApiRunHttpTests
{
    private const string Notes = Origin + "/api/notes";
    private const string Origin = ApiRunHarness.Origin;

    private static string Get(string path, string expect) =>
        """{"requests":[{"method":"GET","path":"PATH","expect":{EXPECT}}]}""".Replace("PATH", path, StringComparison.Ordinal).Replace("EXPECT", expect, StringComparison.Ordinal);

    private static ApiRunHarness With(string script)
    {
        var harness = new ApiRunHarness().WithScript(script);
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, """{"items":[{"title":"a"}]}"""));
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_passes_when_every_expectation_holds()
    {
        var harness = With(Get("/api/notes", """ "status":200,"pointers":[{"pointer":"/items/0/title","equals":"a"},{"pointer":"/items","exists":true},{"pointer":"/items/0/title","matches":"^a$"}]"""));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Equal(0, response.Summary.ExitCode());
        Assert.Equal("passed: 4 assertions", response.Summary.Headline);
        var step = Assert.Single(response.Summary.Steps);
        Assert.Equal((1, ApiAction.Request, "GET /api/notes", ApiStepStatus.Passed, 200), (step.Index, step.Action, step.Target, step.Status, step.Code));
        Assert.Empty(response.Summary.Failures);
        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], harness.Locks.Events);
        Assert.Equal(response.Document, Encoding.UTF8.GetString(harness.Files.Files[ApiRunHarness.SummaryPath]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_fails_with_the_failing_pointer_and_both_values_named()
    {
        var harness = With(Get("/api/notes", """ "status":200,"pointers":[{"pointer":"/items/0/title","equals":"b"}]"""));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Failed, response.Summary.Outcome);
        Assert.Equal(2, response.Summary.ExitCode());
        var failure = Assert.Single(response.Summary.Failures);
        Assert.Equal((1, "/items/0/title", "\"b\"", "\"a\""), (failure.Step, failure.Location, failure.Expected, failure.Actual));
        Assert.Equal("GET /api/notes: /items/0/title is \"a\", expected \"b\" (assertion 2 of 2)", response.Summary.Headline);
        Assert.Equal(ApiStepStatus.Failed, response.Summary.Steps[0].Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_names_a_wrong_status_in_the_headline_and_runs_the_remaining_steps()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":201}},{"method":"GET","path":"/api/notes","expect":{"status":200}}]}""");

        var response = (await harness.Run()).Value;

        Assert.Equal("GET /api/notes: status 200, expected 201 (assertion 1 of 2)", response.Summary.Headline);
        Assert.Equal([ApiStepStatus.Failed, ApiStepStatus.Passed], response.Summary.Steps.Select(static s => s.Status));
        Assert.Equal(2, harness.Transport.Calls.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_fails_a_pointer_assertion_when_the_body_is_not_json()
    {
        var harness = With(Get("/api/notes", """ "status":200,"pointers":[{"pointer":"/a","exists":true}]"""));
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "<html>"));

        var failure = Assert.Single((await harness.Run()).Value.Summary.Failures);

        Assert.Equal("not JSON", failure.Actual);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_non_loopback_host_without_a_preview_pattern()
    {
        var harness = With(Get("/api/notes", "\"status\":200"));
        harness.Settings.PreviewHostPattern = null;

        var response = (await harness.Run(ApiRunHarness.Request("https://app.example.com", allowPreviewHost: true))).Value;

        Assert.Equal(ApiOutcome.Refused, response.Summary.Outcome);
        Assert.Equal(1, response.Summary.ExitCode());
        Assert.Equal(EvidenceErrors.HostRefused.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Transport.Calls);
        Assert.Empty(harness.Locks.Events);
        Assert.Equal("refused", ApiRunHarness.WrittenSummaryOf(response).GetProperty("outcome").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_script_with_requests_when_no_host_is_given()
    {
        var harness = With(Get("/api/notes", "\"status\":200"));

        var response = (await harness.Run(ApiRunHarness.Request(host: null))).Value;

        Assert.Equal(EvidenceErrors.HostRefused.Code, response.Summary.Refusal?.Code);
        Assert.Contains("--host", response.Summary.Refusal?.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_accepts_a_preview_host_with_the_flag_and_sends_any_method()
    {
        var harness = With("""{"requests":[{"method":"DELETE","path":"/api/notes/1","expect":{"status":204}}]}""");
        _ = harness.Transport.Reply("DELETE", "https://pr-12.preview.example.com/api/notes/1", new HttpReply(204, Location: null, string.Empty));

        var response = (await harness.Run(ApiRunHarness.Request("https://pr-12.preview.example.com", allowPreviewHost: true))).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Equal("https://pr-12.preview.example.com", response.Summary.Host);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_sends_a_json_body_and_the_method_the_script_names()
    {
        var harness = With("""{"requests":[{"method":"POST","path":"/api/notes","body":{"title":"hi"},"expect":{"status":201}}]}""");
        _ = harness.Transport.Reply("POST", Notes, new HttpReply(201, Location: null, "{}"));

        _ = await harness.Run();

        var call = Assert.Single(harness.Transport.Calls);
        Assert.Equal((HttpVerb.Post, """{"title":"hi"}"""), (call.Method, call.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_follows_a_same_origin_redirect()
    {
        var harness = With(Get("/old", """ "status":200,"pointers":[{"pointer":"/ok","equals":true}]"""));
        _ = harness.Transport.Reply("GET", Origin + "/old", new HttpReply(302, "/new", string.Empty)).Reply("GET", Origin + "/new", new HttpReply(302, Origin + "/final", string.Empty))
            .Reply("GET", Origin + "/final", new HttpReply(200, Location: null, """{"ok":true}"""));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Equal([Origin + "/old", Origin + "/new", Origin + "/final"], harness.Transport.Calls.Select(static c => c.Url).ToList());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_does_not_follow_a_redirect_when_the_expected_status_is_a_redirect()
    {
        var harness = With(Get("/old", "\"status\":302"));
        _ = harness.Transport.Reply("GET", Origin + "/old", new HttpReply(302, "/new", string.Empty));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        _ = Assert.Single(harness.Transport.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_never_follows_a_redirect_off_the_origin()
    {
        var harness = With(Get("/old", "\"status\":200"));
        _ = harness.Transport.Reply("GET", Origin + "/old", new HttpReply(302, "https://other.example/x", string.Empty));

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Errored, response.Summary.Outcome);
        Assert.Equal(4, response.Summary.ExitCode());
        Assert.Equal(EvidenceErrors.RequestFailed.Code, response.Summary.Error?.Code);
        Assert.Contains("https://other.example", response.Summary.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(ApiStepStatus.Broken, response.Summary.Steps[0].Status);
        _ = Assert.Single(harness.Transport.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_stops_a_redirect_loop_after_five_hops()
    {
        var harness = With(Get("/loop", "\"status\":200"));
        _ = harness.Transport.Reply("GET", Origin + "/loop", new HttpReply(302, "/loop", string.Empty));

        var response = (await harness.Run()).Value;

        Assert.Equal(EvidenceErrors.RequestFailed.Code, response.Summary.Error?.Code);
        Assert.Contains("redirect", response.Summary.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(6, harness.Transport.Calls.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_marks_a_failed_request_errored_and_skips_the_steps_after_it()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}},{"method":"GET","path":"/api/notes","expect":{"status":200}}]}""");
        _ = harness.Transport.Unreachable(Notes);

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Errored, response.Summary.Outcome);
        Assert.Equal([ApiStepStatus.Broken, ApiStepStatus.Skipped], response.Summary.Steps.Select(static s => s.Status));
        Assert.Equal(EvidenceErrors.RequestFailed.Code, response.Summary.Error?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_sends_header_values_from_named_variables_and_records_only_names()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","headers":{"Authorization":{"env":"NOTES_TOKEN"}},"expect":{"status":200}}]}""");
        harness.Environment["NOTES_TOKEN"] = "s3cret-value";

        var response = (await harness.Run()).Value;

        Assert.Equal("s3cret-value", Assert.Single(harness.Transport.Calls).Headers["Authorization"]);
        Assert.Equal(["Authorization"], response.Summary.Steps[0].HeaderNames);
        Assert.DoesNotContain("s3cret-value", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_an_unset_header_variable_by_name_before_any_lock()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","headers":{"Authorization":{"env":"NOTES_TOKEN"}},"expect":{"status":200}}]}""");

        var response = (await harness.Run()).Value;

        Assert.Equal(EvidenceErrors.CredentialsMissing.Code, response.Summary.Refusal?.Code);
        Assert.Contains("NOTES_TOKEN", response.Summary.Refusal?.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Locks.Events);
        Assert.Empty(harness.Transport.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_redacts_and_caps_recorded_bodies()
    {
        var harness = With(Get("/api/notes", "\"status\":200"));
        _ = harness.Scrubber.Hides("tok-123");
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "token tok-123 " + new string('x', 5000)));

        var response = (await harness.Run()).Value;

        var output = response.Summary.Steps[0].Output!;
        Assert.DoesNotContain("tok-123", response.Document, StringComparison.Ordinal);
        Assert.StartsWith("token [REDACTED] ", output, StringComparison.Ordinal);
        Assert.True(output.Length <= 2000);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_keeps_the_headline_to_200_characters_and_scrubbed()
    {
        var harness = With(Get("/api/notes", """ "status":200,"pointers":[{"pointer":"/items/0/title","equals":"tok-123"}]"""));
        _ = harness.Scrubber.Hides("tok-123");
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "{\"items\":[{\"title\":\"" + new string('y', 500) + "\"}]}"));

        var response = (await harness.Run()).Value;

        Assert.True(response.Summary.Headline.Length <= 200);
        Assert.DoesNotContain("tok-123", response.Document, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(DataRunAt.Base, "base")]
    [InlineData(DataRunAt.Head, "head")]
    public async Task ApiRun_records_at_and_app_build(DataRunAt at, string name)
    {
        var harness = With(Get("/api/notes", "\"status\":200"));

        var response = (await harness.Run(ApiRunHarness.Request(at: at, appBuild: "abc123"))).Value;
        var written = ApiRunHarness.WrittenSummaryOf(response);

        Assert.Equal((name, "abc123", "api", "evidence.api.summary/1"), (written.GetProperty("at").GetString(), written.GetProperty("appBuild").GetString(), written.GetProperty("kind").GetString(), written.GetProperty("schema").GetString()));
        Assert.Equal(Origin, written.GetProperty("host").GetString());
        Assert.Equal(64, written.GetProperty("scriptSha256").GetString()!.Length);
        Assert.Equal(JsonValueKind.Array, written.GetProperty("invocations").ValueKind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_deletes_an_earlier_summary_and_writes_none_when_a_lock_stays_busy()
    {
        var harness = With(Get("/api/notes", "\"status\":200"));
        _ = harness.Files.With(ApiRunHarness.SummaryPath, [1]);
        _ = harness.Locks.AlwaysBusy.Add(Core.Locks.LockKind.QaStack);

        var result = await harness.Run();

        Assert.Equal(EvidenceErrors.LockBusy.Code, result.Error.Code);
        Assert.False(harness.Files.Files.ContainsKey(ApiRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_writes_no_summary_when_the_script_cannot_be_read()
    {
        var harness = new ApiRunHarness();

        var result = await harness.Run();

        Assert.Equal(EvidenceErrors.FileNotFound.Code, result.Error.Code);
        Assert.False(harness.Files.Files.ContainsKey(ApiRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_an_invalid_script_with_a_refused_summary()
    {
        var harness = new ApiRunHarness().WithScript("""{"requests":[{"method":"GET","path":"http://other.example/x","expect":{"status":200}}]}""");

        var response = (await harness.Run()).Value;

        Assert.Equal(EvidenceErrors.ApiScriptInvalid.Code, response.Summary.Refusal?.Code);
        Assert.Contains("script /work/api.json: ", response.Summary.Refusal?.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_checks_a_golden_file_next_to_the_script()
    {
        var harness = With(Get("/api/notes", """ "status":200,"golden":"notes.json" """));
        _ = harness.Files.With("/work/notes.json", Encoding.UTF8.GetBytes("""{ "items": [ { "title": "a" } ] }"""));

        var passed = (await harness.Run()).Value;
        _ = harness.Files.With("/work/notes.json", Encoding.UTF8.GetBytes("""{"items":[]}"""));
        var failed = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Passed, passed.Summary.Outcome);
        Assert.Equal(ApiOutcome.Failed, failed.Summary.Outcome);
        Assert.Contains("notes.json", Assert.Single(failed.Summary.Failures).Expected, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_missing_golden_file_before_any_lock()
    {
        var harness = With(Get("/api/notes", """ "status":200,"golden":"missing.json" """));

        var response = (await harness.Run()).Value;

        Assert.Equal(EvidenceErrors.FileNotFound.Code, response.Summary.Refusal?.Code);
        Assert.Empty(harness.Locks.Events);
    }

    private const string SecretBody = """{"password":"s3cr3t","user":{"name":"ann","api_key":"k1","tokens":[{"secret":"x9"},{"id":7}]},"list":[{"password":"p2"}]}""";

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_the_value_of_every_secret_named_member_in_a_recorded_body_at_any_depth()
    {
        var harness = With(Get("/api/notes", "\"status\":201"));
        _ = harness.Scrubber.HidesName("password").HidesName("api_key").HidesName("secret");
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, SecretBody));

        var response = (await harness.Run()).Value;

        foreach (var value in new[] { "s3cr3t", "\"k1\"", "x9", "\"p2\"" })
        {
            Assert.DoesNotContain(value, response.Document, StringComparison.Ordinal);
        }

        var output = response.Summary.Steps[0].Output!;
        Assert.Contains("\"name\":\"ann\"", output, StringComparison.Ordinal);
        Assert.Contains("\"id\":7", output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_hides_the_quoted_values_of_a_pointer_assertion_through_a_secret_named_member_in_failures_and_headline()
    {
        var harness = With(Get("/api/notes", """ "status":200,"pointers":[{"pointer":"/user/api_key","equals":"wanted-k"},{"pointer":"/list/0/password","equals":"wanted-p"}]"""));
        _ = harness.Scrubber.HidesName("password").HidesName("api_key");
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, SecretBody));

        var response = (await harness.Run()).Value;

        Assert.Equal(2, response.Summary.Failures.Count);
        Assert.All(response.Summary.Failures, static failure => Assert.Equal("[REDACTED:secret-field]", failure.Actual));
        foreach (var value in new[] { "\"k1\"", "p2", "wanted-k", "wanted-p" })
        {
            Assert.DoesNotContain(value, response.Document, StringComparison.Ordinal);
        }

        Assert.Contains("/user/api_key", response.Summary.Headline, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_leaves_a_body_that_is_not_json_to_the_value_scrubbing()
    {
        var harness = With(Get("/api/notes", "\"status\":200"));
        _ = harness.Scrubber.HidesName("password").Hides("tok-123");
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "password=short tok-123 {broken"));

        var response = (await harness.Run()).Value;

        Assert.Equal("password=short [REDACTED] {broken", response.Summary.Steps[0].Output);
    }
}
