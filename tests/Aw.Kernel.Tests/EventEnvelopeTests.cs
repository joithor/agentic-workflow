using Aw.Kernel.Events;
using Aw.Kernel.Ids;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class EventEnvelopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid Correlation = new("00000000-0000-0000-0000-0000000000c1");

    [Fact]
    public void An_envelope_takes_its_id_and_time_from_the_ports_and_its_who_from_the_context()
    {
        var context = EventContext.Create("host:sindri", "personal", 0, Correlation, traceId: "trace-1").Value;

        var envelope = context.Envelope("item.started", new Started("item-7"), new FixedClock(Now), new SequentialIds());

        Assert.Equal(SequentialIds.First, envelope.Id);
        Assert.Equal("item.started", envelope.Type);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(Now, envelope.OccurredAt);
        Assert.Equal(("host:sindri", "personal", 0), (envelope.Actor, envelope.Tenant, envelope.Ring));
        Assert.Equal(Correlation, envelope.CorrelationId);
        Assert.Null(envelope.CausationId);
        Assert.Equal("trace-1", envelope.TraceId);
        Assert.Equal(new Started("item-7"), envelope.Payload);
    }

    [Fact]
    public void An_event_caused_by_another_keeps_the_correlation_and_points_at_its_cause()
    {
        var ids = new SequentialIds();
        var clock = new FixedClock(Now);
        var context = EventContext.Create("host:sindri", "personal", 0, Correlation).Value;
        var cause = context.Envelope("item.started", new Started("item-7"), clock, ids);

        var effect = context.CausedBy(cause).Envelope("item.parked", new Started("item-7"), clock, ids);

        Assert.Equal(cause.Id, effect.CausationId);
        Assert.Equal(Correlation, effect.CorrelationId);
        Assert.NotEqual(cause.Id, effect.Id);
    }

    [Theory]
    [InlineData("", "personal", 0)]
    [InlineData("host:sindri", " ", 0)]
    [InlineData("host:sindri", "personal", -1)]
    [InlineData("host:sindri", "personal", 3)]
    public void An_incomplete_context_is_an_invalid_error(string actor, string tenant, int ring)
    {
        var result = EventContext.Create(actor, tenant, ring, Correlation);

        Assert.Equal("AW-KERNEL-001", result.Error.Code);
    }

    private sealed record Started(string Item);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public static readonly Guid First = new("00000000-0000-0000-0000-000000000001");
        private int _next;

        public Guid NewId() => new($"00000000-0000-0000-0000-{++_next:D12}");
    }
}
