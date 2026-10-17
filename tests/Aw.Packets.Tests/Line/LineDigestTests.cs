using System.Text;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Line;

public sealed class LineDigestTests
{
    private static async Task<LinePacketFiles> Read(PacketKind kind, Dictionary<string, byte[]> files) =>
        (await new InMemoryLinePacketSource().With("pkt", files).Read(kind, "pkt", CancellationToken.None)).Value;

    private static async Task<string> Hash(PacketKind kind, Dictionary<string, byte[]> files) => PacketDigest.Compute(kind, await Read(kind, files)).Value.Value;

    [Fact, Trait("Lane", "unit")]
    public async Task The_digest_of_the_gold_review_packet_is_pinned() =>
        // computed independently of the C# code (python: sorted keys, no whitespace, packet.json without status and hash):
        // digestVersion 1, a change to these bytes needs a new PacketDigest.Version, not a new pin.
        Assert.Equal("sha256:49fd7a218097b784d8be9f5376e09dcb75e860cae39012f1bfe6850a39f281a6", await Hash(PacketKinds.Review, A.LinePacket(PacketKinds.Review).Build()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task The_hash_covers_packet_json_and_index_json_not_just_the_content_files()
    {
        var kind = PacketKinds.Review;
        var plain = await Hash(kind, A.LinePacket(kind).Build());

        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).Edit("packet.json", "expiry-review", "other-review").Build()), StringComparer.Ordinal);
        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).Edit("index.json", "\"parents\": []", $"\"parents\": [\"{LinePacketBuilder.OtherHash}\"]").Build()), StringComparer.Ordinal);
        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).With("review.json", "{ \"schemaVersion\": 1, \"head\": \"x\" }").Build()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_hash_formatting_key_order_and_the_schema_hint_do_not_change_the_hash()
    {
        var kind = PacketKinds.Handoff;
        var draft = await Hash(kind, A.LinePacket(kind).Build());
        var frozen = await Hash(kind, A.LinePacket(kind)
            .Edit("packet.json", "\"status\": \"draft\", \"hash\": null", $"\"status\": \"frozen\", \"hash\": \"{LinePacketBuilder.Hash}\"")
            .Edit("packet.json", "{ \"schemaVersion\": 1,", "{ \"$schema\": \"../x.json\", \"schemaVersion\": 1,").Build());

        Assert.Equal(draft, frozen, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_text_or_jsonl_file_hashes_by_its_bytes()
    {
        var kind = PacketKinds.Design;
        var plain = await Hash(kind, A.LinePacket(kind).Build());

        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).With("spec.md", "# Text \n").Build()), StringComparer.Ordinal);
        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).With("lens-rounds.jsonl", "{ \"round\": 1 }\n").Build()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_files_under_a_linked_directory_are_hashed_by_content_and_unlisted_entries_are_not()
    {
        var kind = PacketKinds.Proof;
        var plain = await Hash(kind, A.LinePacket(kind).Build());

        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).With("evidence/shot.txt", "other").Build()), StringComparer.Ordinal);
        Assert.NotEqual(plain, await Hash(kind, A.LinePacket(kind).With("app-run/more.txt", "more").Build()), StringComparer.Ordinal);
        Assert.Equal(plain, await Hash(kind, A.LinePacket(kind).With("notes.txt", "x").Build()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_kind_is_part_of_the_hashed_document()
    {
        // the same bytes hashed as two kinds must differ, so no packet of one kind can stand in for another
        var files = await Read(PacketKinds.Review, A.LinePacket(PacketKinds.Review).Build());
        var asReview = PacketDigest.Compute(PacketKinds.Review, files).Value;
        var fake = new PacketKind("other", PacketKinds.Review.Files);

        Assert.NotEqual(asReview, PacketDigest.Compute(fake, files).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_file_or_a_duplicate_key_has_no_hash()
    {
        var missing = PacketDigest.Compute(PacketKinds.Plan, await Read(PacketKinds.Plan, A.LinePacket(PacketKinds.Plan).Without("graph.json").Build()));
        var duplicate = PacketDigest.Compute(PacketKinds.Plan, await Read(PacketKinds.Plan, A.LinePacket(PacketKinds.Plan).With("graph.json", Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}")).Build()));

        Assert.Equal("AW-PACKETS-003", missing.Error.Code);
        Assert.Contains("graph.json", missing.Error.Message, StringComparison.Ordinal);
        Assert.Equal("AW-PACKETS-003", duplicate.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_registry_lists_every_kind_by_name_and_only_the_line_kinds_are_line_kinds()
    {
        Assert.Equal(["build", "scoping", "design", "plan", "phase", "proof", "review", "handoff"], PacketKinds.All.Select(static k => k.Name), StringComparer.Ordinal);
        Assert.Equal(["design", "plan", "phase", "proof", "review", "handoff"], PacketKinds.All.Where(static k => k.IsLine).Select(static k => k.Name), StringComparer.Ordinal);
        Assert.All(PacketKinds.All.Where(static k => k.IsLine), static k =>
        {
            Assert.False(k.IsManifestDriven);
            Assert.Equal("packet.json", k.Files[0].Name);
            Assert.Equal("index.json", k.Files[^1].Name);
            Assert.Equal(k.Files.Count, k.Files.Select(static f => f.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.NotNull(LineRules.For(k));
        });
        Assert.Null(LineRules.For(PacketKinds.Scoping));
        Assert.Equal(["spec.md", "sections.json"], PacketKinds.Design.Files.Skip(1).Take(2).Select(static f => f.Name), StringComparer.Ordinal);
        Assert.Equal(["app-run", "evidence"], PacketKinds.Proof.Directories.Select(static d => d.Name), StringComparer.Ordinal);
    }
}
