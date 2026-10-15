using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Events;

public sealed class PublicEventsTests
{
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public void Command_handled_is_never_public()
    {
        var row = Row(EventTypes.CommandHandled, """{"commandId":"x"}""");

        Assert.Null(PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_ledger_read_sees_command_handled_with_its_whole_payload()
    {
        var row = Row(EventTypes.CommandHandled, """{"caller":"human:cli"}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: true).Value;

        Assert.NotNull(shown);
        Assert.Equal("human:cli", shown.Payload.GetProperty("caller").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Public_payload_drops_fields_the_public_record_lacks()
    {
        var row = Row(EventTypes.ProcessStarted, """{"host":"host-1","pid":7,"epoch":3,"profile":null,"private":"only for the ledger"}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        Assert.False(shown.Payload.TryGetProperty("private", out _));
        Assert.Equal(7, shown.Payload.GetProperty("pid").GetInt32());
    }

    [Fact, Trait("Lane", "unit")]
    public void Ansi_and_osc_in_a_payload_arrive_escaped()
    {
        var title = "x\u001b]8;;http://e\u0007y\u001b[31mz";
        var created = new ItemCreated(_h.Ids.New<CommandId>(), A.FirstItemId, title, Repo: null, Link: null, Body: "body", Owner: null);
        var entry = _h.Events.Entry(EventTypes.ItemCreated, created, "item.created:x", A.FirstItemId.Value);
        var row = new LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        var text = shown.Payload.GetProperty("title").GetString()!;
        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain('\u0007', text);
        Assert.Contains("\\x1B", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unparseable_payload_is_sent_with_a_null_payload()
    {
        var row = Row(EventTypes.ProcessStarted, "{not json");

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        Assert.Equal(System.Text.Json.JsonValueKind.Null, shown.Payload.ValueKind);
        Assert.Equal(row.Sequence, shown.Sequence);
    }

    [Fact, Trait("Lane", "unit")]
    public void Envelope_fields_match_the_ledger_row()
    {
        var row = Row(EventTypes.ProcessStarted, """{"host":"host-1","pid":7,"epoch":3,"profile":null}""") with { Sequence = 42, Item = A.FirstItemId.Value };

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        var e = row.Envelope;
        Assert.Equal((42L, e.Id, e.Type, e.SchemaVersion, e.OccurredAt, e.Actor, e.Tenant, e.Ring),
            (shown.Sequence, shown.Id.Value, shown.Type, shown.SchemaVersion, shown.OccurredAt, shown.Actor, shown.Tenant, shown.Ring));
        Assert.Equal(A.FirstItemId, shown.Item);
        Assert.Equal(e.CorrelationId, shown.CorrelationId.Value);
    }

    private LedgerEvent Row(string type, string payload)
    {
        var entry = _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k");
        return new LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope with { Type = type, Payload = payload }, IdempotencyKey: "k", Item: null);
    }
}
