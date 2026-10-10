using System.Text;
using Aw.Packets.Core.Checks;
using Aw.Packets.App.Features.ValidatePacket;
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
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)));

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
}
