using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Locks;

namespace Aw.Evidence.Tests.ApiRun;

/// <summary>Where api-run stops early: a summary is written on every exit but a lock timeout, and a stale one never survives (evidence.md "summary.json for api-run").</summary>
public sealed class ApiRunStopTests
{
    private const string Notes = ApiRunHarness.Origin + "/api/notes";

    private static ApiRunHarness With(string script)
    {
        var harness = new ApiRunHarness().WithScript(script);
        _ = harness.Transport.Reply("GET", Notes, new HttpReply(200, Location: null, "{}"));
        return harness;
    }

    private const string OneRequest = """{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}}]}""";

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_deletes_an_earlier_summary_even_when_the_script_cannot_be_read()
    {
        var harness = new ApiRunHarness();
        _ = harness.Files.With(ApiRunHarness.SummaryPath, [1]);

        var result = await harness.Run();

        Assert.Equal(EvidenceErrors.FileNotFound.Code, result.Error.Code);
        Assert.False(harness.Files.Files.ContainsKey(ApiRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_writes_an_errored_summary_when_a_lock_fails_for_a_reason_other_than_busy()
    {
        var harness = With(OneRequest);
        _ = harness.Files.With(ApiRunHarness.SummaryPath, [1]);
        _ = harness.Locks.Fails.Add(LockKind.QaStack);

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Errored, response.Summary.Outcome);
        Assert.Equal(4, response.Summary.ExitCode());
        Assert.Equal(EvidenceErrors.FileUnavailable.Code, response.Summary.Error?.Code);
        Assert.Null(response.Summary.Refusal);
        Assert.Empty(harness.Transport.Calls);
        Assert.Equal(response.Document, Encoding.UTF8.GetString(harness.Files.Files[ApiRunHarness.SummaryPath]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_records_an_unreadable_golden_file_as_errored_exit_4_not_refused()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200,"golden":"notes.json"}}]}""");
        _ = harness.Files.Broken.Add("/work/notes.json");

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Errored, response.Summary.Outcome);
        Assert.Equal(4, response.Summary.ExitCode());
        Assert.Equal(EvidenceErrors.FileUnavailable.Code, response.Summary.Error?.Code);
        Assert.Null(response.Summary.Refusal);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_refuses_a_missing_golden_file_with_exit_1()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200,"golden":"notes.json"}}]}""");

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Refused, response.Summary.Outcome);
        Assert.Equal(1, response.Summary.ExitCode());
        Assert.Equal(EvidenceErrors.FileNotFound.Code, response.Summary.Refusal?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApiRun_records_no_steps_for_a_refused_run_because_nothing_ran()
    {
        var harness = With("""{"requests":[{"method":"GET","path":"/api/notes","expect":{"status":200}},{"method":"GET","path":"/api/notes","headers":{"Authorization":{"env":"NOTES_TOKEN"}},"expect":{"status":200}}]}""");

        var response = (await harness.Run()).Value;

        Assert.Equal(ApiOutcome.Refused, response.Summary.Outcome);
        Assert.Empty(response.Summary.Steps);
        Assert.Empty(harness.Transport.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_has_no_refused_status_because_refusals_are_recorded_on_the_run() =>
        Assert.DoesNotContain("Refused", Enum.GetNames<ApiStepStatus>(), StringComparer.Ordinal);
}
