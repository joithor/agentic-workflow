using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A Step input's fill is given room to finish and a heartbeat to guard against a stall, and a timeout is named, never a bare 'failed' (#962).</summary>
[Lane(TestLane.Unit)]
public sealed class InputFillPolicyTests
{
    private const string Input = "input.context.lenses";

    [Fact]
    public void A_fill_gets_at_least_five_minutes_where_it_had_one()
    {
        var options = InputFillPolicy.Options("inputs:t:1", "q");

        Assert.True(options.StartToCloseTimeout >= TimeSpan.FromMinutes(5));
        Assert.Equal(options.StartToCloseTimeout, options.ScheduleToCloseTimeout);   // a fill that ran out its time is not run again
    }

    [Fact]
    public void A_stalled_fill_is_caught_by_a_heartbeat_at_least_five_beats_wide()
    {
        var options = InputFillPolicy.Options("inputs:t:1", "q");

        Assert.True(options.HeartbeatTimeout >= InputFillPolicy.BeatEvery * 5);
        Assert.True(options.HeartbeatTimeout < options.StartToCloseTimeout);
    }

    [Fact]
    public void A_transient_failure_waits_at_least_five_times_the_old_backoff_before_trying_again()
    {
        var retry = InputFillPolicy.Options("inputs:t:1", "q").RetryPolicy!;

        Assert.True(retry.InitialInterval >= TimeSpan.FromSeconds(5));
        Assert.True(retry.MaximumAttempts >= 2);
    }

    [Fact]
    public void A_start_to_close_timeout_names_the_code_and_the_seconds()
    {
        var reason = InputFillPolicy.Reason(Input, Timeout(TimeoutType.StartToClose));

        Assert.StartsWith($"input {Input} timed out {WorkflowErrors.StepInputTimedOut.Code}", reason, StringComparison.Ordinal);
        Assert.Contains("after 1800 s", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_heartbeat_timeout_says_it_stopped_reporting_progress()
    {
        var reason = InputFillPolicy.Reason(Input, Timeout(TimeoutType.Heartbeat));

        Assert.Contains(WorkflowErrors.StepInputTimedOut.Code, reason, StringComparison.Ordinal);
        Assert.Contains("no progress for 120 s", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("terminated")]
    [InlineData("canceled")]
    public void Other_non_application_failures_are_named_not_called_failed(string word)
    {
        var failure = new Failure { Message = word };
        if (word == "terminated")
        {
            failure.TerminatedFailureInfo = new TerminatedFailureInfo();
        }
        else
        {
            failure.CanceledFailureInfo = new CanceledFailureInfo();
        }

        Assert.Equal($"input {Input} failed: {word}", InputFillPolicy.Reason(Input, Raised(failure)));
    }

    [Fact]
    public void An_application_failure_keeps_its_type_and_never_its_message()
    {
        var inner = new ApplicationFailureException("secret text", errorType: "InvalidOperationException");

        Assert.Equal($"input {Input} failed: InvalidOperationException", InputFillPolicy.Reason(Input, inner));
    }

    private static Exception Raised(Failure failure) => DataConverter.Default.FailureConverter.ToException(failure, DataConverter.Default.PayloadConverter);

    private static Exception Timeout(TimeoutType type) => Raised(new Failure { Message = "timeout", TimeoutFailureInfo = new TimeoutFailureInfo { TimeoutType = type } });
}
