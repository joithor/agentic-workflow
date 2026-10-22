using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.EngineReads;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A Temporal history event as the engine in depth shows it (API spec §3.8, E-30): its type, the activity, timer or signal it concerns and one line of what happened. A local-activity
/// marker of 'RecordEvent' carries that activity's id, which is the 'temporal_event_id' of the ledger row it wrote (engine spec §5.6), so the history line can name its ledger sequence.
/// </summary>
internal static class EngineHistoryEvents
{
    private const string LocalActivityMarker = "core_local_activity";

    private const string RecordEvent = "RecordEvent";

    /// <summary>The facts of one event.</summary>
    /// <param name="historyEvent">The event.</param>
    /// <returns>Its facts.</returns>
    internal static EngineEventFacts Facts(HistoryEvent historyEvent)
    {
        ArgumentNullException.ThrowIfNull(historyEvent);
        var (name, summary, activity) = Describe(historyEvent);
        return new EngineEventFacts(historyEvent.EventId, historyEvent.EventTime.ToDateTimeOffset(), historyEvent.EventType.ToString(), name, summary ?? Words(historyEvent.EventType.ToString()), activity);
    }

    private static (string? Name, string? Summary, string? Activity) Describe(HistoryEvent e)
    {
        if (e.EventType == EventType.ActivityTaskScheduled)
        {
            var name = e.ActivityTaskScheduledEventAttributes.ActivityType.Name;
            return (name, $"scheduled {name}", null);
        }

        if (e.EventType == EventType.ActivityTaskStarted)
        {
            return (null, Attempt(e.ActivityTaskStartedEventAttributes), null);
        }

        if (e.EventType == EventType.ActivityTaskFailed)
        {
            return (null, $"failed: {e.ActivityTaskFailedEventAttributes.Failure?.Message}", null);
        }

        if (e.EventType == EventType.TimerStarted)
        {
            return ($"timer-{e.TimerStartedEventAttributes.TimerId}", string.Create(CultureInfo.InvariantCulture, $"timer set for {e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan().TotalSeconds:0.#} s"), null);
        }

        if (e.EventType == EventType.WorkflowExecutionSignaled)
        {
            return (e.WorkflowExecutionSignaledEventAttributes.SignalName, $"signal {e.WorkflowExecutionSignaledEventAttributes.SignalName}", null);
        }

        return e.EventType == EventType.MarkerRecorded ? Marker(e.MarkerRecordedEventAttributes) : (null, null, null);
    }

    private static string Attempt(ActivityTaskStartedEventAttributes started) => started.LastFailure is { } failure
        ? string.Create(CultureInfo.InvariantCulture, $"attempt {started.Attempt} · retry after {failure.Message}")
        : string.Create(CultureInfo.InvariantCulture, $"attempt {started.Attempt}");

    private static (string? Name, string? Summary, string? Activity) Marker(MarkerRecordedEventAttributes marker)
    {
        if (!string.Equals(marker.MarkerName, LocalActivityMarker, StringComparison.Ordinal) || !marker.Details.TryGetValue("data", out var payloads) || payloads.Payloads_.Count == 0)
        {
            return (marker.MarkerName, $"marker {marker.MarkerName}", null);
        }

        try
        {
            using var data = JsonDocument.Parse(Encoding.UTF8.GetString(payloads.Payloads_[0].Data.ToByteArray()));
            var type = Text(data.RootElement, "activity_type");
            var activity = Text(data.RootElement, "activity_id");
            return (type, $"local activity {type}", string.Equals(type, RecordEvent, StringComparison.Ordinal) ? activity : null);
        }
        catch (JsonException) // boundary: a marker of some other shape is a marker, not a mirrored row
        {
            return (marker.MarkerName, $"marker {marker.MarkerName}", null);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>'ActivityTaskScheduled' as 'activity task scheduled'.</summary>
    /// <param name="type">An event type's name.</param>
    /// <returns>Its words.</returns>
    internal static string Words(string type)
    {
        var words = new StringBuilder(type.Length + 4);
        foreach (var c in type)
        {
            _ = char.IsUpper(c) && words.Length > 0 ? words.Append(' ').Append(char.ToLowerInvariant(c)) : words.Append(words.Length == 0 ? char.ToLowerInvariant(c) : c);
        }

        return words.ToString();
    }
}
