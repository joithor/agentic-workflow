using System.Globalization;
using Aw.Sindri.Core.Workflow;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// How long a Step input's fill may run and how a failed one is reported. A fill reports that it is alive every <see cref="BeatEvery"/>, so only a
/// silent one fails (after <see cref="Silence"/>); <see cref="Limit"/> bounds the whole fill, retries included, and is the only fixed time limit left.
/// </summary>
internal static class InputFillPolicy
{
    /// <summary>How often a running fill reports that it is alive.</summary>
    internal static readonly TimeSpan BeatEvery = TimeSpan.FromSeconds(15);

    /// <summary>How long a fill may go without reporting before it counts as stalled (eight beats).</summary>
    internal static readonly TimeSpan Silence = TimeSpan.FromMinutes(2);

    /// <summary>The longest one fill may run, all attempts together, so a deterministic slow fill that ran out its time is not run again.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(30);

    /// <summary>The options of one input's 'FillInput' activity.</summary>
    /// <param name="activityId">The activity id.</param>
    /// <param name="taskQueue">The task queue.</param>
    /// <returns>The options.</returns>
    internal static ActivityOptions Options(string activityId, string taskQueue) => new()
    {
        ActivityId = activityId,
        TaskQueue = taskQueue,
        StartToCloseTimeout = Limit,
        ScheduleToCloseTimeout = Limit,
        HeartbeatTimeout = Silence,
        // only a transient failure (a thrown input, a worker that went quiet) is tried again; a refusal is non-retryable at the activity
        RetryPolicy = new RetryPolicy { MaximumAttempts = 3, InitialInterval = TimeSpan.FromSeconds(10), BackoffCoefficient = 3, MaximumInterval = TimeSpan.FromMinutes(1) },
    };

    /// <summary>The Step's failure reason for an input whose activity failed with <paramref name="inner"/>: a code, never the failure's message (it could carry text).</summary>
    /// <param name="input">The input block id.</param>
    /// <param name="inner">The activity failure's cause.</param>
    /// <returns>The reason.</returns>
    internal static string Reason(string input, Exception? inner) => inner switch
    {
        TimeoutFailureException { TimeoutType: TimeoutType.Heartbeat } =>
            string.Create(CultureInfo.InvariantCulture, $"input {input} timed out {WorkflowErrors.StepInputTimedOut.Code} (no progress for {Seconds(Silence)} s)"),
        TimeoutFailureException =>
            string.Create(CultureInfo.InvariantCulture, $"input {input} timed out {WorkflowErrors.StepInputTimedOut.Code} after {Seconds(Limit)} s (no progress for {Seconds(Silence)} s would have failed it sooner)"),
        ApplicationFailureException { ErrorType: { } type } => $"input {input} failed: {type}",
        TerminatedFailureException => $"input {input} failed: terminated",
        CanceledFailureException => $"input {input} failed: canceled",
        _ => $"input {input} failed: failed",
    };

    private static int Seconds(TimeSpan span) => (int)span.TotalSeconds;
}
