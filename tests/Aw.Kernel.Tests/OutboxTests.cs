using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class OutboxTests
{
    [Fact]
    public void The_table_is_outbox_commands_so_it_never_collides_with_the_judge_outbox() =>
        Assert.Equal("outbox_commands", OutboxTables.Commands);

    [Fact]
    public void A_command_and_an_error_serialize_with_camel_case_keys_in_declaration_order()
    {
        var options = NodeJson.CreateOptions(KernelJsonContext.Default);
        var command = new OutboxCommand(
            new Guid("00000000-0000-0000-0000-0000000000aa"),
            "github.comment",
            "{\"pr\":1}",
            new DateTimeOffset(2026, 10, 10, 6, 0, 0, TimeSpan.Zero),
            "host:sindri",
            new Guid("00000000-0000-0000-0000-0000000000c1"));

        Assert.Equal(
            "{\"id\":\"00000000-0000-0000-0000-0000000000aa\",\"kind\":\"github.comment\",\"payloadJson\":\"{\\\"pr\\\":1}\","
            + "\"enqueuedAt\":\"2026-10-10T06:00:00.000Z\",\"actor\":\"host:sindri\",\"correlationId\":\"00000000-0000-0000-0000-0000000000c1\"}",
            Encoding.UTF8.GetString(NodeJson.Serialize(command, options)));
        Assert.Equal(
            "{\"code\":\"AW-KERNEL-002\",\"kind\":\"notFound\",\"message\":\"gone\"}",
            Encoding.UTF8.GetString(NodeJson.Serialize(KernelErrors.NotFound.With("gone"), options)));
    }
}
