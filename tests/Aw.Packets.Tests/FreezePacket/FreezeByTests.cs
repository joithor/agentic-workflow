using System.Text;
using Aw.Kernel.Json;
using Aw.Packets.App.Features.FreezeLinePacket;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests.FreezePacket;

/// <summary>LN-PKT5: who froze a packet, stored as 'by' in the freeze record. The digest never sees the record.</summary>
public sealed class FreezeByTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static async Task<FreezeRecord> FreezeBuild(FreezeBy? by)
    {
        var log = new InMemoryFreezeLog();
        var inspector = new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(A.Packet().Build())),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles));
        var request = by is { } given ? new FreezePacketRequest("pkt", RepoDirectory: null, By: given) : new FreezePacketRequest("pkt");
        var result = await new FreezePacketService(inspector, log, new FakeTimeProvider(_now)).Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return Assert.Single(log.Records).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_freeze_is_by_the_machine_unless_the_owner_asked()
    {
        Assert.Equal(FreezeBy.Machine, (await FreezeBuild(by: null)).By);
        Assert.Equal(FreezeBy.Machine, (await FreezeBuild(FreezeBy.Machine)).By);
        Assert.Equal(FreezeBy.Owner, (await FreezeBuild(FreezeBy.Owner)).By);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Who_froze_it_is_not_part_of_the_hash()
    {
        var byMachine = await FreezeBuild(FreezeBy.Machine);
        var byOwner = await FreezeBuild(FreezeBy.Owner);

        Assert.Equal(byMachine.Hash, byOwner.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_line_freeze_records_who_froze_it()
    {
        var log = new InMemoryFreezeLog();
        var inspector = new LinePacketInspector(
            new InMemoryLinePacketSource().With("pkt", A.LinePacket(PacketKinds.Design).Build()),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles),
            PacketFiles.Json);

        var result = await new FreezeLinePacketService(inspector, log, new FakeTimeProvider(_now))
            .Handle(new FreezeLinePacketRequest("design", "pkt", RepoDirectory: null, By: FreezeBy.Owner), CancellationToken.None);

        Assert.Equal(FreezeBy.Owner, result.Value.By);
        Assert.Equal(FreezeBy.Owner, Assert.Single(log.Records).Value.By);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scoping_freeze_records_who_froze_it()
    {
        var log = new InMemoryFreezeLog();
        var source = new InMemoryScopingPacketSource().With("pkt", A.ScopingPacket().Build());

        var result = await new FreezeScopingPacketService(source, log, new FakeTimeProvider(_now), PacketFiles.Json)
            .Handle(new FreezeScopingPacketRequest("pkt", "example/repo", "idea-1", FreezeBy.Owner), CancellationToken.None);

        Assert.Equal(FreezeBy.Owner, result.Value.By);
        Assert.Equal(FreezeBy.Owner, Assert.Single(log.Records).Value.By);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_writes_by_and_an_old_record_without_it_reads_as_a_machine_freeze()
    {
        var options = AwJson.CreateOptions(App.PacketsJsonContext.Default, indented: true);
        var record = new FreezeRecord
        {
            SchemaVersion = 1,
            Repo = Core.Values.RepoSlug.From("example/repo"),
            Packet = Core.Ids.PacketId.From("demo-packet"),
            Hash = Core.Values.PacketHash.From("sha256:" + new string('a', 64)),
            DigestVersion = 1,
            By = FreezeBy.Owner,
            FrozenAt = _now,
            Files = ["packet.json"],
        };

        var written = Encoding.UTF8.GetString(AwJson.Serialize(record, options));
        var old = written.Replace("  \"by\": \"owner\",\n", string.Empty, StringComparison.Ordinal);
        var read = AwJson.Deserialize<FreezeRecord>(Encoding.UTF8.GetBytes(old), options);

        Assert.Contains("\"by\": \"owner\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\"by\"", old, StringComparison.Ordinal);
        Assert.Equal(FreezeBy.Machine, read.Value.By);
    }
}
