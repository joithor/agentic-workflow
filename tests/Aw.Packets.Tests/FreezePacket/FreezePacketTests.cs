using System.Text;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests.FreezePacket;

public sealed class FreezePacketTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static (FreezePacketService Service, InMemoryFreezeLog Log, InMemoryPacketSource Source) Setup(
        Dictionary<string, byte[]> files, InMemoryFreezeLog? log = null)
    {
        var source = new InMemoryPacketSource().With("pkt", files);
        var freezeLog = log ?? new InMemoryFreezeLog();
        var inspector = new PacketInspector(source, new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles));
        return (new FreezePacketService(inspector, freezeLog, new FakeTimeProvider(_now)), freezeLog, source);
    }

    private static async Task<FreezePacketResponse> Freeze(Dictionary<string, byte[]> files)
    {
        var result = await Setup(files).Service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private static Dictionary<string, byte[]> Edit(Dictionary<string, byte[]> files, string path, string from, string to)
    {
        var copy = files.ToDictionary(StringComparer.Ordinal);
        copy[path] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[path]).Replace(from, to, StringComparison.Ordinal));
        return copy;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task FreezePacket_records_the_canonical_hash_under_packets()
    {
        var (service, log, _) = Setup(PacketFiles.Of(A.Packet().Build()));

        var result = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        var frozen = result.Value;
        Assert.True(frozen.Frozen);
        Assert.False(frozen.AlreadyFrozen);
        var hash = Assert.NotNull(frozen.Hash);
        Assert.Equal($"example/repo/demo-packet/frozen/{hash.Hex}.json", frozen.Location);
        var record = Assert.Single(log.Records).Value;
        Assert.Equal((hash, _now, PacketBuilder.BaseSha, 1), (record.Hash, record.FrozenAt, record.BaseSha?.Value, record.DigestVersion));
        Assert.Equal(["decisions.json", "design/design.md", "out-of-scope.json", "packet.json", "tasks.json"], record.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Freezing_again_is_already_frozen_with_the_same_hash()
    {
        var (service, log, _) = Setup(PacketFiles.Of(A.Packet().Build()));

        var first = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);
        var second = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal(first.Value.Hash, second.Value.Hash);
        Assert.True(second.Value.AlreadyFrozen);
        _ = Assert.Single(log.Records);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_hash_ignores_formatting_key_order_status_and_hash_but_not_content()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        var hash = (await Freeze(files)).Hash;

        var compact = files.ToDictionary(static f => f.Key, static f => f.Key.EndsWith(".json", StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Value).Replace("\n", "", StringComparison.Ordinal).Replace("  ", "", StringComparison.Ordinal))
            : f.Value, StringComparer.Ordinal);
        var reordered = Edit(files, "out-of-scope.json", "\"schemaVersion\": 1,\n  \"items\": [\n    \"anything else\"\n  ]", "\"items\": [\"anything else\"], \"schemaVersion\": 1");
        var restatused = Edit(Edit(files, "packet.json", "\"draft\"", "\"frozen\""), "packet.json", "\"hash\": null", "\"hash\": \"sha256:" + new string('a', 64) + "\"");

        Assert.Equal(hash, (await Freeze(compact)).Hash);
        Assert.Equal(hash, (await Freeze(reordered)).Hash);
        Assert.Equal(hash, (await Freeze(restatused)).Hash);
        Assert.NotEqual(hash, (await Freeze(Edit(files, "out-of-scope.json", "anything else", "anything other"))).Hash);
        Assert.NotEqual(hash, (await Freeze(Edit(files, "design/design.md", "#", "##"))).Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_that_fails_a_check_isnt_frozen_and_nothing_is_recorded()
    {
        var (service, log, _) = Setup(PacketFiles.Of(A.Packet().With(A.Task("t1").After("t1")).Build()));

        var result = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        Assert.False(result.Value.Frozen);
        Assert.Null(result.Value.Hash);
        Assert.Null(result.Value.Location);
        Assert.Contains(result.Value.Checks, static c => c.Rule == PacketRules.Acyclic && c.State == CheckState.Fail);
        Assert.Empty(log.Records);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unwritable_freeze_log_is_AW_PACKETS_007()
    {
        var (service, _, _) = Setup(PacketFiles.Of(A.Packet().Build()), new InMemoryFreezeLog { Unwritable = true });

        var result = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal(PacketsErrors.FreezeLogUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_load_error_passes_through()
    {
        var (service, _, _) = Setup([]);

        var result = await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal(PacketsErrors.NotFound.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_duplicate_key_is_an_error_not_a_crash()
    {
        var files = Edit(PacketFiles.Of(A.Packet().Build()), "decisions.json", "\"text\": \"Which shape?\",", "\"text\": \"Which shape?\", \"text\": \"Which?\",");

        var result = await Setup(files).Service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_schema_hint_is_not_part_of_the_hash()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        var hash = (await Freeze(files)).Hash;
        var hinted = files.ToDictionary(static f => f.Key, static f => f.Key.EndsWith(".json", StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes("{\n  \"$schema\": \"../schemas/x.json\"," + Encoding.UTF8.GetString(f.Value)[1..])
            : f.Value, StringComparer.Ordinal);

        Assert.Equal(hash, (await Freeze(hinted)).Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_record_lives_under_the_repo_and_the_packet()
    {
        var (service, _, _) = Setup(PacketFiles.Of(A.Packet().Build()));

        var frozen = (await service.Handle(new FreezePacketRequest("pkt"), CancellationToken.None)).Value;

        Assert.StartsWith("example/repo/demo-packet/frozen/", frozen.Location, StringComparison.Ordinal);
    }
}
