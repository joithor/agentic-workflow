using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>
/// 'workflow events' (engine spec §5.6, §6.3; README X16) reads the ledger, never Temporal's history: an instance's
/// events are the rows it mirrored under 'workflow:{address}:…'; a child's and another instance's are left out. The
/// same filter is S1-D6's '?instance=' on the event stream, with '?item='.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceEventsTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/step/1");

    [Fact]
    public async Task Events_are_the_instances_own_ledger_rows_with_their_transitions()
    {
        var h = await Ledger();

        var read = (await new GetInstanceEventsService(h.Store, h.Events).Handle(new GetInstanceEventsRequest(_address), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 4L], read.Events.Select(static e => e.Sequence));
        Assert.Equal(5, read.Cursor);
        Assert.Equal(new InstanceEventFields("produce", "checks", "step.completed", "done", "1.1"), read.Events[1].Transition);
        var at = read.Events[0].OccurredAt.UtcDateTime.ToString("MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var actor = read.Events[0].Actor;
        Assert.Equal(
            $"2  {at}  workflow.started  -> produce  start  {actor}\n4  {at}  workflow.transition  produce -> checks  step.completed done  run 1.1  {actor}\n",
            EventsCommand.Lines(read.Events, empty: true));
    }

    [Fact]
    public async Task After_the_cursor_there_are_no_events_and_status_says_so()
    {
        var h = await Ledger();

        var read = (await new GetInstanceEventsService(h.Store, h.Events).Handle(new GetInstanceEventsRequest(_address, After: 5), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((0, 5L, "no events\n"), (read.Events.Count, read.Cursor, EventsCommand.Lines(read.Events, empty: true)));
    }

    [Fact]
    public async Task The_event_stream_filters_by_instance_or_by_item_and_reports_how_far_it_read()
    {
        var h = await Ledger();
        var service = new StreamEventsService(h.Store);
        var ct = TestContext.Current.CancellationToken;

        var byInstance = (await service.Handle(new StreamEventsRequest(0, Instance: _address), ct)).Value;
        var byItem = (await service.Handle(new StreamEventsRequest(0, Item: ItemKey.From("T-1")), ct)).Value;
        var unfiltered = (await service.Handle(new StreamEventsRequest(0), ct)).Value;

        Assert.Equal([2L, 4L], byInstance.Events.Select(static e => e.Sequence));
        Assert.Equal(5L, byInstance.Scanned);
        Assert.Equal([2L, 3L, 4L], byItem.Events.Select(static e => e.Sequence));
        Assert.Equal(5, unfiltered.Events.Count);
    }

    /// <summary>Five rows: a heartbeat, T-1/step/1's start, its child's start, its transition, and T-2/step/1's start.</summary>
    private static async Task<SliceHarness> Ledger()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        (string Type, string Key, string? Item, InstanceEventFields Fields)[] rows =
        [
            ("service.heartbeat", "service.heartbeat:1:1", null, InstanceEventFields.None),
            ("workflow.started", "workflow:T-1/step/1:run-a:1", "T-1", new(string.Empty, "produce", "start")),
            ("workflow.started", "workflow:T-1/step/1/step.2:run-b:1", "T-1", new(string.Empty, "produce", "start")),
            ("workflow.transition", "workflow:T-1/step/1:run-a:3", "T-1", new("produce", "checks", "step.completed", "done", "1.1")),
            ("workflow.started", "workflow:T-2/step/1:run-c:1", "T-2", new(string.Empty, "produce", "start")),
        ];
        foreach (var (type, key, item, fields) in rows)
        {
            var entry = h.Events.Entry(type, fields, key) with { Item = item };
            Assert.False((await h.Store.Append(entry, grant, ct)).IsError);
        }

        return h;
    }
}
