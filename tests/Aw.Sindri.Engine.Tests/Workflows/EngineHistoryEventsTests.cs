using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A history event as the engine in depth shows it (API spec §3.8, E-30): the marker of a RecordEvent local activity names the activity id the ledger row records, and open timers are read from a history.</summary>
[Lane(TestLane.Unit)]
public sealed class EngineHistoryEventsTests
{
    private static HistoryEvent Marker(string name, string? data)
    {
        var marker = new MarkerRecordedEventAttributes { MarkerName = name };
        if (data is not null)
        {
            var payloads = new Payloads();
            payloads.Payloads_.Add(new Payload { Data = ByteString.CopyFromUtf8(data) });
            marker.Details["data"] = payloads;
        }

        return new HistoryEvent { EventId = 9, EventTime = Timestamp.FromDateTimeOffset(KnownInstants.Epoch), EventType = EventType.MarkerRecorded, MarkerRecordedEventAttributes = marker };
    }

    private static HistoryEvent Timer(long id, EventType type, string timerId, int seconds = 0)
    {
        var e = new HistoryEvent { EventId = id, EventTime = Timestamp.FromDateTimeOffset(KnownInstants.Epoch.AddSeconds(id)), EventType = type };
        switch (type)
        {
            case EventType.TimerStarted:
                e.TimerStartedEventAttributes = new TimerStartedEventAttributes { TimerId = timerId, StartToFireTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(seconds)) };
                break;
            case EventType.TimerFired:
                e.TimerFiredEventAttributes = new TimerFiredEventAttributes { TimerId = timerId };
                break;
            default:
                e.TimerCanceledEventAttributes = new TimerCanceledEventAttributes { TimerId = timerId };
                break;
        }

        return e;
    }

    [Fact]
    public void A_RecordEvent_marker_carries_its_activity_id()
    {
        var facts = EngineHistoryEvents.Facts(Marker("core_local_activity", """{"seq":3,"attempt":1,"activity_id":"7","activity_type":"RecordEvent"}"""));

        Assert.Equal(("MarkerRecorded", "RecordEvent", "7"), (facts.Type, facts.Name, facts.Activity));
        Assert.Equal(9L, facts.EventId);
    }

    [Fact]
    public void A_local_activity_of_another_type_names_no_activity_id()
    {
        var facts = EngineHistoryEvents.Facts(Marker("core_local_activity", """{"activity_id":"7","activity_type":"Other"}"""));

        Assert.Null(facts.Activity);
    }

    [Theory]
    [InlineData("some_marker", "{}")]
    [InlineData("core_local_activity", "not json")]
    [InlineData("core_local_activity", null)]
    public void Any_other_marker_names_no_activity_id(string name, string? data) =>
        Assert.Null(EngineHistoryEvents.Facts(Marker(name, data)).Activity);

    [Fact]
    public void A_timer_started_and_neither_fired_nor_cancelled_is_open_and_the_others_are_not()
    {
        IReadOnlyList<HistoryEvent> newestFirst =
        [
            Timer(8, EventType.TimerCanceled, "3"),
            Timer(7, EventType.TimerStarted, "4", 300),
            Timer(6, EventType.TimerFired, "2"),
            Timer(3, EventType.TimerStarted, "3", 60),
            Timer(2, EventType.TimerStarted, "2", 10),
        ];

        var open = EngineInstanceScan.OpenTimers(newestFirst);

        var timer = Assert.Single(open);
        Assert.Equal(("timer-4", "timeout", KnownInstants.Epoch.AddSeconds(7 + 300)), (timer.Name, timer.Kind, timer.FiresAt));
    }
}
