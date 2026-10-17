using System.Text;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

public sealed class LoadingTests
{
    private static async Task<string> ErrorCode(Action<Dictionary<string, byte[]>> breakIt)
    {
        var files = PacketFiles.Of(A.Packet().Build());
        breakIt(files);
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        return result.IsError ? result.Error.Code : "none";
    }

    private static void Replace(Dictionary<string, byte[]> files, string path, string from, string to) =>
        files[path] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[path]).Replace(from, to, StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public async Task No_packet_json_is_AW_PACKETS_002() =>
        Assert.Equal("AW-PACKETS-002", await ErrorCode(static f => f.Remove("packet.json")));

    [Fact, Trait("Lane", "unit")]
    public async Task Malformed_json_is_AW_PACKETS_003() =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(static f => f["tasks.json"] = "{"u8.ToArray()));

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_required_field_is_AW_PACKETS_003() =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(static f => Replace(f, "packet.json", "\"baseSha\"", "\"baseShaX\"")));

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_linked_file_is_AW_PACKETS_003() =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(static f => f.Remove("design/design.md")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("../escape.json")]
    [InlineData("/abs.json")]
    [InlineData("a/./b.json")]
    [InlineData("a\\\\b.json")]
    [InlineData("packet.json")]
    public async Task A_link_outside_the_packet_directory_is_AW_PACKETS_003(string link) =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(f => Replace(f, "packet.json", "\"decisions.json\"", $"\"{link}\"")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("packet.json")]
    [InlineData("tasks.json")]
    [InlineData("decisions.json")]
    [InlineData("out-of-scope.json")]
    public async Task An_unsupported_schema_version_is_AW_PACKETS_004(string file) =>
        Assert.Equal("AW-PACKETS-004", await ErrorCode(f => Replace(f, file, "\"schemaVersion\": 1", "\"schemaVersion\": 2")));

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_typed_id_is_AW_PACKETS_003() =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(static f => Replace(f, "packet.json", "\"demo-packet\"", "\"Demo Packet\"")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("tasks.json", "\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"schemaVersion\": 1,")]
    [InlineData("decisions.json", "\"text\": \"Which shape?\",", "\"text\": \"Which shape?\", \"text\": \"Which?\",")]
    public async Task A_duplicate_key_is_AW_PACKETS_003(string file, string from, string to) =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(f => Replace(f, file, from, to)));

    [Fact, Trait("Lane", "unit")]
    public async Task A_byte_order_mark_is_named_in_the_error()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        files["tasks.json"] = [0xEF, 0xBB, 0xBF, .. files["tasks.json"]];
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal("AW-PACKETS-003", result.Error.Code);
        Assert.Contains("byte order mark", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("tasks.json", "\"writeScope\": [", "\"writeScope\": [null, ")]
    [InlineData("tasks.json", "\"evidence\": [", "\"evidence\": [null, ")]
    [InlineData("tasks.json", "\"tasks\": [", "\"tasks\": [null, ")]
    [InlineData("tasks.json", "\"readSeeds\": [", "\"readSeeds\": [null, ")]
    [InlineData("decisions.json", "\"questions\": [", "\"questions\": [null, ")]
    [InlineData("out-of-scope.json", "\"items\": [", "\"items\": [null, ")]
    [InlineData("packet.json", "\"design\": [", "\"design\": [null, ")]
    public async Task A_null_in_any_list_is_AW_PACKETS_003_naming_where(string file, string from, string to)
    {
        var files = PacketFiles.Of(A.Packet().Build());
        Replace(files, file, from, to);
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal("AW-PACKETS-003", result.Error.Code);
        Assert.Contains("null", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("tasks.json", "\"holdout\": true", "\"heldOut\": true")]
    [InlineData("packet.json", "\"repo\":", "\"repository\": \"x/y\", \"repo\":")]
    [InlineData("decisions.json", "\"answer\":", "\"answr\": \"x\", \"answer\":")]
    public async Task An_unknown_member_is_AW_PACKETS_003(string file, string from, string to) =>
        Assert.Equal("AW-PACKETS-003", await ErrorCode(f => Replace(f, file, from, to)));

    [Fact, Trait("Lane", "unit")]
    public async Task A_schema_hint_is_allowed_in_every_file() =>
        Assert.Equal("none", await ErrorCode(static f =>
        {
            foreach (var file in (string[])["packet.json", "decisions.json", "out-of-scope.json", "tasks.json"])
            {
                Replace(f, file, "{\n  \"schemaVersion\"", "{\n  \"$schema\": \"x.json\",\n  \"schemaVersion\"");
            }
        }));

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_null_in_document_order_is_named()
    {
        var files = PacketFiles.Of(A.Packet().With(A.Task("t1").Writing("src/a.cs")).With(A.Task("t2").After("t1").Writing("src/b.cs")).Build());
        Replace(files, "tasks.json", "\"readSeeds\": [", "\"readSeeds\": [null, ");
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", files),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.Contains("$.tasks[0].readSeeds[0]", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_over_the_task_limit_is_AW_PACKETS_003()
    {
        var packet = A.Packet();
        for (var i = 0; i <= PacketReader.MaxTasks; i++)
        {
            _ = packet.With(A.Task("t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Writing("src/a.cs"));
        }

        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build())),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());

        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal("AW-PACKETS-003", result.Error.Code);
        Assert.Contains("split", result.Error.Message, StringComparison.Ordinal);
    }
}
