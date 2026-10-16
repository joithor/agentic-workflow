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
    public void A_human_ledger_read_sees_a_public_types_whole_payload_scrubbed_and_escaped()
    {
        var created = new ItemCreated(_h.Ids.New<CommandId>(), A.FirstItemId, "title", Repo: null, Link: null, Body: "private \u001b[31mSECRET\u001b[0m body", Owner: null);
        var entry = _h.Events.Entry(EventTypes.ItemCreated, created, "item.created:x", A.FirstItemId.Value);
        var row = new LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);

        var shown = PublicEvents.For(row, SliceHarness.Json, new MaskingScrubber(), includeNonPublic: true).Value!;

        Assert.Equal("private \\x1B[31m[masked]\\x1B[0m body", shown.Payload.GetProperty("body").GetString());   // the stream's public record leaves the body out; the human's audit read doesn't
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
    public void A_payload_with_an_array_of_objects_arrives_scrubbed_not_thrown_on()
    {
        var row = Row(EventTypes.ContextSourcesSynced,
            """{"source":"repo-docs","slot":"2026-10-15T00:00:00+00:00","repos":[{"repo":"aSECRETb","head":null,"added":1,"removed":0,"kept":2,"blocked":0,"code":null}]}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new MaskingScrubber(), includeNonPublic: false).Value!;

        Assert.Equal("a[masked]b", shown.Payload.GetProperty("repos")[0].GetProperty("repo").GetString());
        Assert.Equal(1, shown.Payload.GetProperty("repos")[0].GetProperty("added").GetInt32());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_links_block_of_a_synced_event_arrives_scrubbed()
    {
        var row = Row(EventTypes.ContextSourcesSynced,
            """{"source":"repo-docs","slot":"2026-10-15T00:00:00+00:00","repos":[],"links":{"records":3,"fetched":2,"unchanged":1,"linked":4,"unlinked":0,"orphaned":0,"decisions":1,"queued":0,"refused":0,"tokens":9,"code":"xSECRETy"}}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new MaskingScrubber(), includeNonPublic: false).Value!;

        var links = shown.Payload.GetProperty("links");
        Assert.Equal(3, links.GetProperty("records").GetInt32());
        Assert.Equal("x[masked]y", links.GetProperty("code").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_read_scrubs_strings_in_nested_objects_and_arrays_beside_numbers_and_booleans()
    {
        var row = Row(EventTypes.ContextSourcesSynced,
            """{"source":"repo-docs","inner":{"note":"aSECRETb","n":[1,true,"cSECRETd",[2,"eSECRETf"],{"k":"gSECRETh"}]}}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new MaskingScrubber(), includeNonPublic: true).Value!;

        var inner = shown.Payload.GetProperty("inner");
        var items = inner.GetProperty("n");
        Assert.Equal("a[masked]b", inner.GetProperty("note").GetString());
        Assert.Equal(1, items[0].GetInt32());
        Assert.True(items[1].GetBoolean());
        Assert.Equal("c[masked]d", items[2].GetString());
        Assert.Equal("e[masked]f", items[3][1].GetString());
        Assert.Equal("g[masked]h", items[4].GetProperty("k").GetString());
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

    private sealed class MaskingScrubber : Core.Text.ITextScrubber
    {
        public string Scrub(string text) => text.Replace("SECRET", "[masked]", StringComparison.Ordinal);
    }
}
