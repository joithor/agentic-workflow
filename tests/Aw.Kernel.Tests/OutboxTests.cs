using System.Text;
using Aw.Kernel.Events;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class OutboxTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid _correlation = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xc1);

    [Fact]
    public void The_table_is_outbox_commands_so_it_never_collides_with_the_judge_outbox() =>
        Assert.Equal("outbox_commands", OutboxTables.Commands);

    [Fact]
    public void A_command_takes_actor_tenant_ring_correlation_causation_and_trace_from_the_context()
    {
        var ids = new CountingIds();
        var context = EventContext.Create("host:sindri", "personal", 1, _correlation, traceId: "trace-1").Value;
        var cause = context.Envelope("item.started", new Shapes.ItemStarted("item-7"), new FixedClock(_now), ids);

        var command = context.CausedBy(cause).Command("github.comment", "{\"pr\":1}", new FixedClock(_now), ids);

        Assert.Equal(
            new OutboxCommand(CountingIds.Id(2), "github.comment", "{\"pr\":1}", OutboxCommand.CurrentSchemaVersion, _now,
                "host:sindri", "personal", 1, _correlation, cause.Id, "trace-1"),
            command);
    }

    [Fact]
    public void A_command_and_an_error_serialize_with_camel_case_keys_in_declaration_order()
    {
        var options = NodeJson.CreateOptions(TestJsonContext.Default);
        var command = new OutboxCommand(CountingIds.Id(0xaa), "github.comment", "{\"pr\":1}", 1, _now, "host:sindri",
            "personal", 0, _correlation, CausationId: null, TraceId: null);

        Assert.Equal(
            "{\"id\":\"00000000-0000-0000-0000-0000000000aa\",\"kind\":\"github.comment\",\"payloadJson\":\"{\\\"pr\\\":1}\","
            + "\"schemaVersion\":1,\"enqueuedAt\":\"2026-10-10T06:00:00.000Z\",\"actor\":\"host:sindri\",\"tenant\":\"personal\","
            + "\"ring\":0,\"correlationId\":\"00000000-0000-0000-0000-0000000000c1\"}",
            Encoding.UTF8.GetString(NodeJson.Serialize(command, options)));
        Assert.Equal(
            "{\"code\":\"AW-KERNEL-002\",\"kind\":\"notFound\",\"message\":\"gone\"}",
            Encoding.UTF8.GetString(NodeJson.Serialize(KernelErrors.NotFound.With("gone"), options)));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(4, 240)]
    [InlineData(7, 3600)]
    public void A_failed_attempt_is_retried_with_doubling_delay_up_to_the_cap(int failedAttempts, int delaySeconds) =>
        Assert.Equal(_now.AddSeconds(delaySeconds), OutboxRetryPolicy.Default.NextAttempt(failedAttempts, _now));

    [Fact]
    public void After_the_last_attempt_the_command_is_dead_lettered() =>
        Assert.Null(OutboxRetryPolicy.Default.NextAttempt(OutboxRetryPolicy.Default.MaxAttempts, _now));

    [Fact]
    public void A_retry_policy_with_out_of_range_values_is_an_invalid_error()
    {
        Assert.Equal("AW-KERNEL-001", OutboxRetryPolicy.Create(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Error.Code);
        Assert.Equal("AW-KERNEL-001", OutboxRetryPolicy.Create(3, TimeSpan.Zero, TimeSpan.FromSeconds(1)).Error.Code);
        Assert.Equal("AW-KERNEL-001", OutboxRetryPolicy.Create(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)).Error.Code);
    }

    [Fact]
    public void Counting_attempts_from_zero_is_a_bug() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryPolicy.Default.NextAttempt(0, _now));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CountingIds : IIdGenerator
    {
        private int _next;

        public static Guid Id(int n) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)n);

        public Guid NewId() => Id(++_next);
    }
}
