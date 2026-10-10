using System.Text.Json;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class JsonLinesTests
{
    [Fact]
    public void A_log_call_writes_one_json_line_with_time_level_category_event_and_message()
    {
        using var h = new Harness();

        var logger = h.Logger("Aw.Demo.Start");

        Log.ItemStarted(logger, "item-1", 42);

        var line = Assert.Single(h.Lines());
        Assert.Equal("2026-10-10T06:00:00.0000000Z", line.GetProperty("timestamp").GetString());
        Assert.Equal("information", line.GetProperty("level").GetString());
        Assert.Equal("Aw.Demo.Start", line.GetProperty("category").GetString());
        Assert.Equal(7, line.GetProperty("eventId").GetInt32());
        Assert.Equal("ItemStarted", line.GetProperty("eventName").GetString());
        Assert.Equal("Started item-1 in 42 ms", line.GetProperty("message").GetString());
        Assert.Equal("item-1", line.GetProperty("fields").GetProperty("Item").GetString());
        Assert.Equal("42", line.GetProperty("fields").GetProperty("Ms").GetString());
    }

    [Fact]
    public void Each_line_is_a_single_line_even_when_the_message_has_line_breaks()
    {
        using var h = new Harness();

        h.Logger().Emit(LogLevel.Warning, "first\nsecond\r\nthird");

        var raw = Assert.Single(h.Sink.Lines);
        Assert.DoesNotContain('\n', raw);
        Assert.Equal("first\nsecond\r\nthird", JsonDocument.Parse(raw).RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Lines_below_the_minimum_level_are_not_written()
    {
        using var h = new Harness(level: LogLevel.Warning);
        var logger = h.Logger();

        logger.Emit(LogLevel.Information, "quiet");
        logger.Emit(LogLevel.Warning, "loud");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.Equal("loud", Assert.Single(h.Lines()).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(LogLevel.Trace, "trace")]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Warning, "warning")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Critical, "critical")]
    public void Levels_are_written_as_lowercase_names(LogLevel level, string name)
    {
        using var h = new Harness(level: LogLevel.Trace);

        h.Logger().Emit(level, "x");

        Assert.Equal(name, Assert.Single(h.Lines()).GetProperty("level").GetString());
    }

    [Fact]
    public void Inside_a_span_the_line_carries_its_trace_and_span_ids()
    {
        using var h = new Harness();
        using var span = h.Source.StartActivity("work");

        h.Logger().Emit(LogLevel.Information, "inside");

        var line = Assert.Single(h.Lines());
        Assert.NotNull(span);
        Assert.Equal(span.TraceId.ToHexString(), line.GetProperty("traceId").GetString());
        Assert.Equal(span.SpanId.ToHexString(), line.GetProperty("spanId").GetString());
    }

    [Fact]
    public void Outside_a_span_the_ids_are_null()
    {
        using var h = new Harness();

        h.Logger().Emit(LogLevel.Information, "outside");

        var line = Assert.Single(h.Lines());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("traceId").ValueKind);
        Assert.Equal(JsonValueKind.Null, line.GetProperty("spanId").ValueKind);
    }

    [Fact]
    public void An_event_context_scope_adds_actor_tenant_ring_and_correlation()
    {
        using var h = new Harness();
        var correlation = new Guid(0x0f8fad5b, 0xd9cb, 0x469f, 0xa1, 0x65, 0x70, 0x86, 0x77, 0x28, 0x95, 0x0e);
        var context = EventContext.Create("host:demo", "personal", 1, correlation).Value;
        var logger = h.Logger();

        using (logger.BeginScope(context))
        {
            logger.Emit(LogLevel.Information, "scoped");
        }

        logger.Emit(LogLevel.Information, "unscoped");

        var lines = h.Lines();
        Assert.Equal("host:demo", lines[0].GetProperty("actor").GetString());
        Assert.Equal("personal", lines[0].GetProperty("tenant").GetString());
        Assert.Equal(1, lines[0].GetProperty("ring").GetInt32());
        Assert.Equal(correlation.ToString(), lines[0].GetProperty("correlationId").GetString());
        Assert.False(lines[1].TryGetProperty("actor", out _));
    }

    [Fact]
    public void An_exception_is_written_as_its_type_and_message()
    {
        using var h = new Harness();

        var logger = h.Logger();

        Log.Failed(logger, new InvalidOperationException("boom"));

        var exception = Assert.Single(h.Lines()).GetProperty("exception").GetString();
        Assert.NotNull(exception);
        Assert.StartsWith("System.InvalidOperationException: boom", exception, StringComparison.Ordinal);
    }

    [Fact]
    public void A_repeated_field_name_gets_a_numbered_key_so_no_property_repeats()
    {
        using var h = new Harness();
        h.Logger().EmitTemplate("{A} then {A}", ("A", 1), ("A", 2));

        var fields = Assert.Single(h.Lines()).GetProperty("fields");
        Assert.Equal("1", fields.GetProperty("A").GetString());
        Assert.Equal("2", fields.GetProperty("A_2").GetString());
    }

    [Fact]
    public void A_numbered_key_never_collides_with_a_real_field_name()
    {
        using var h = new Harness();

        h.Logger().EmitTemplate("{A} {A_2} {A}", ("A", 1), ("A_2", 2), ("A", 3));

        var raw = Assert.Single(h.Sink.Lines);
        var fields = JsonDocument.Parse(raw).RootElement.GetProperty("fields").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(fields.Count, fields.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, fields.Count);
    }
}
