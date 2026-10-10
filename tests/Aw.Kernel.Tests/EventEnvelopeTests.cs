using Aw.Kernel.Events;
using Aw.Kernel.Ids;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class EventEnvelopeTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid _correlation = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xc1);

    [Fact]
    public void An_envelope_takes_its_id_and_time_from_the_ports_and_its_who_from_the_context()
    {
        var context = EventContext.Create("host:sindri", "personal", 0, _correlation, traceId: "trace-1").Value;

        var envelope = context.Envelope("item.started", new Started("item-7"), new FixedClock(_now), new SequentialIds());

        Assert.Equal(SequentialIds.First, envelope.Id);
        Assert.Equal("item.started", envelope.Type);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(_now, envelope.OccurredAt);
        Assert.Equal(("host:sindri", "personal", 0), (envelope.Actor, envelope.Tenant, envelope.Ring));
        Assert.Equal(_correlation, envelope.CorrelationId);
        Assert.Null(envelope.CausationId);
        Assert.Equal("trace-1", envelope.TraceId);
        Assert.Equal(new Started("item-7"), envelope.Payload);
    }

    [Fact]
    public void An_event_caused_by_another_keeps_the_correlation_and_points_at_its_cause()
    {
        var ids = new SequentialIds();
        var clock = new FixedClock(_now);
        var context = EventContext.Create("host:sindri", "personal", 0, _correlation).Value;
        var cause = context.Envelope("item.started", new Started("item-7"), clock, ids);

        var effect = context.CausedBy(cause).Envelope("item.parked", new Started("item-7"), clock, ids);

        Assert.Equal(cause.Id, effect.CausationId);
        Assert.Equal(_correlation, effect.CorrelationId);
        Assert.NotEqual(cause.Id, effect.Id);
    }

    [Fact]
    public void An_envelope_serializes_with_camel_case_names_in_declaration_order_and_explicit_nulls()
    {
        var context = EventContext.Create("host:sindri", "personal", 0, _correlation).Value;
        var envelope = context.Envelope("item.started", new Shapes.ItemStarted("item-7"), new FixedClock(_now), new SequentialIds());

        Assert.Equal(
            "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"type\":\"item.started\",\"schemaVersion\":1,"
            + "\"occurredAt\":\"2026-10-10T06:00:00.0000000Z\",\"actor\":\"host:sindri\",\"tenant\":\"personal\",\"ring\":0,"
            + "\"correlationId\":\"00000000-0000-0000-0000-0000000000c1\",\"causationId\":null,\"traceId\":null,"
            + "\"payload\":{\"item\":\"item-7\"}}",
            System.Text.Encoding.UTF8.GetString(Json.AwJson.Serialize(envelope, Json.AwJson.CreateOptions(TestJsonContext.Default))));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData("has space")]
    public void A_blank_or_spaced_trace_id_is_an_invalid_error(string traceId) =>
        Assert.Equal("AW-KERNEL-001", EventContext.Create("host:sindri", "personal", 0, _correlation, traceId).Error.Code);

    [Fact]
    public void An_empty_correlation_id_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", EventContext.Create("host:sindri", "personal", 0, Guid.Empty).Error.Code);

    [Theory]
    [InlineData("", "personal", 0)]
    [InlineData("host:sindri", " ", 0)]
    [InlineData("host:sindri", "personal", -1)]
    [InlineData("host:sindri", "personal", 3)]
    public void An_incomplete_context_is_an_invalid_error(string actor, string tenant, int ring)
    {
        var result = EventContext.Create(actor, tenant, ring, _correlation);

        Assert.Equal("AW-KERNEL-001", result.Error.Code);
    }

    private sealed record Started(string Item);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public static readonly Guid First = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        private int _next;

        public Guid NewId() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)++_next);
    }
}
