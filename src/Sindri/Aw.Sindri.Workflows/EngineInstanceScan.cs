using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Google.Protobuf;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using ApiExecution = Temporalio.Api.Common.V1.WorkflowExecution;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What one running recipe instance waits on, read from Temporal (API spec §3.8, E-30): its pending activity or child from the execution's description, and its running timers and the
/// time it last settled from the newest events of its history. An instance with no pending activity, child or timer waits for a signal (an update arrives through the same wait), since the
/// time its last workflow task completed.
/// </summary>
internal static class EngineInstanceScan
{
    /// <summary>How many of the newest events are read to find the open timers and when the instance last settled.</summary>
    private const int Recent = 100;

    /// <summary>The facts of the instance <paramref name="execution"/> lists; null when it isn't a Sindri recipe instance (no recipe id, or an address that isn't one).</summary>
    /// <param name="client">The client.</param>
    /// <param name="settings">The namespace.</param>
    /// <param name="execution">The visibility row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or null.</returns>
    internal static async Task<EngineInstanceFacts?> Read(ITemporalClient client, TemporalSettings settings, WorkflowExecution execution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(execution);
        var attributes = execution.TypedSearchAttributes;
        if (!InstanceAddress.TryFrom(execution.Id, out var address) || !attributes.TryGetValue(SindriTemporal.RecipeKey, out var recipe) || !RecipeId.TryFrom(recipe, out var recipeId))
        {
            return null;
        }

        var described = await TemporalReads.Run(rpc => client.GetWorkflowHandle(execution.Id).DescribeAsync(new() { Rpc = rpc }), cancellationToken).ConfigureAwait(false);
        var events = await Newest(client, settings, execution.Id, cancellationToken).ConfigureAwait(false);
        var timers = OpenTimers(events);
        var settled = events.FirstOrDefault(static e => e.EventType == EventType.WorkflowTaskCompleted) ?? events.FirstOrDefault();
        var since = settled?.EventTime.ToDateTimeOffset() ?? new DateTimeOffset(DateTime.SpecifyKind(execution.StartTime, DateTimeKind.Utc));
        var pending = described.RawDescription.PendingActivities.FirstOrDefault();
        return new EngineInstanceFacts(
            address,
            new EngineRecipeRef(recipeId, attributes.TryGetValue(SindriTemporal.RecipeHashKey, out var hash) ? hash : null),
            attributes.TryGetValue(SindriTemporal.StateKey, out var state) ? state : null,
            Waiting(described.RawDescription, pending, timers, since),
            timers,
            pending is { Attempt: > 1 } retrying ? new EngineRetries(retrying.Attempt, retrying.LastFailure?.Message) : null,
            described.HistoryLength);
    }

    private static EngineWaiting Waiting(DescribeWorkflowExecutionResponse description, Temporalio.Api.Workflow.V1.PendingActivityInfo? activity, List<EngineTimer> timers, DateTimeOffset since) =>
        activity is not null
            ? new EngineWaiting(EngineWaitKinds.Activity, activity.ActivityType.Name, (activity.LastStartedTime ?? activity.ScheduledTime)?.ToDateTimeOffset() ?? since)
            : description.PendingChildren.Count > 0
                ? new EngineWaiting(EngineWaitKinds.Child, null, since)
                : timers.Count > 0
                    ? new EngineWaiting(EngineWaitKinds.Timer, timers[0].Name, since)
                    : new EngineWaiting(EngineWaitKinds.Signal, null, since);

    /// <summary>The timers started and neither fired nor cancelled in <paramref name="newestFirst"/>.</summary>
    /// <param name="newestFirst">The newest events, newest first.</param>
    /// <returns>The open timers, oldest first.</returns>
    internal static List<EngineTimer> OpenTimers(IReadOnlyList<HistoryEvent> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        var closed = new HashSet<string>(StringComparer.Ordinal);
        var open = new List<EngineTimer>();
        foreach (var e in newestFirst)
        {
            switch (e.EventType)
            {
                case EventType.TimerFired:
                    _ = closed.Add(e.TimerFiredEventAttributes.TimerId);
                    break;
                case EventType.TimerCanceled:
                    _ = closed.Add(e.TimerCanceledEventAttributes.TimerId);
                    break;
                case EventType.TimerStarted when !closed.Contains(e.TimerStartedEventAttributes.TimerId):
                    var started = e.TimerStartedEventAttributes;
                    open.Add(new EngineTimer($"timer-{started.TimerId}", e.EventTime.ToDateTimeOffset() + started.StartToFireTimeout.ToTimeSpan(), "timeout"));
                    break;
                default:
                    break;
            }
        }

        open.Reverse();
        return open;
    }

    private static async Task<List<HistoryEvent>> Newest(ITemporalClient client, TemporalSettings settings, string workflowId, CancellationToken cancellationToken)
    {
        var request = new GetWorkflowExecutionHistoryReverseRequest { Namespace = settings.Namespace, Execution = new ApiExecution { WorkflowId = workflowId }, MaximumPageSize = Recent, NextPageToken = ByteString.Empty };
        var response = await TemporalReads.Run(rpc => client.WorkflowService.GetWorkflowExecutionHistoryReverseAsync(request, rpc), cancellationToken).ConfigureAwait(false);
        return [.. response.History.Events];
    }
}
