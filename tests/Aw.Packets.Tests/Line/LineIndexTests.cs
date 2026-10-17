using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Line;

/// <summary>index.json ties a packet to its acceptance.json (S3), superseded items name a successor (S5), and read errors name the file, not a CLR type (N9).</summary>
public sealed class LineIndexTests
{
    private static IEnumerable<CheckResult> Failures(IEnumerable<CheckResult> checks, string rule) =>
        checks.Where(c => c.Rule == rule && c.State == CheckState.Fail);

    private static string Acceptance(string items) =>
        $"{{ \"schemaVersion\": 1, \"parent\": \"{LinePacketBuilder.OtherHash}\", \"items\": [{items}] }}\n";

    private const string Failing = "{ \"id\": \"AC-1\", \"text\": \"t\", \"status\": \"failing\", \"evidence\": [] }";

    [Theory, Trait("Lane", "unit")]
    [InlineData("design")]
    [InlineData("phase")]
    [InlineData("proof")]
    public async Task The_index_carries_the_hash_of_acceptance_json_and_its_parent(string name)
    {
        var kind = PacketKinds.Find(name)!;
        var prefix = name == "design" ? "PKT-DSN" : name == "phase" ? "PKT-PHS" : "PKT-PRF";
        var wrongSelf = A.LinePacket(kind).With("index.json", LinePacketBuilder.Index(name, "[]", $"\"{LinePacketBuilder.Hash}\"", $"\"{LinePacketBuilder.OtherHash}\"")).Build();
        var wrongExtends = A.LinePacket(kind).Build();
        wrongExtends["index.json"] = System.Text.Encoding.UTF8.GetBytes(LinePacketBuilder.Index(name, "[]", $"\"{LinePacketBuilder.FileHash(wrongExtends["acceptance.json"])}\"", $"\"{LinePacketBuilder.Hash}\""));
        var noSelf = A.LinePacket(kind).With("index.json", LinePacketBuilder.Index(name, "[]", "null", $"\"{LinePacketBuilder.OtherHash}\"")).Build();

        var gold = await LineChecksTests.Run(kind, A.LinePacket(kind).Build());
        var self = await LineChecksTests.Run(kind, wrongSelf);
        var extends = await LineChecksTests.Run(kind, wrongExtends);
        var none = await LineChecksTests.Run(kind, noSelf);

        Assert.Empty(Failures(gold, $"{prefix}-006"));
        Assert.Contains("acceptance.self", Assert.Single(Failures(self, $"{prefix}-006")).Subject, StringComparison.Ordinal);
        Assert.Contains("acceptance.extends", Assert.Single(Failures(extends, $"{prefix}-006")).Subject, StringComparison.Ordinal);
        Assert.Contains("acceptance.self", Assert.Single(Failures(none, $"{prefix}-006")).Subject, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("plan", "PKT-PLN")]
    [InlineData("review", "PKT-REV")]
    [InlineData("handoff", "PKT-HND")]
    public async Task A_kind_with_no_acceptance_json_has_a_null_self(string name, string prefix)
    {
        var kind = PacketKinds.Find(name)!;

        var set = await LineChecksTests.Run(kind, A.LinePacket(kind).With("index.json", LinePacketBuilder.Index(name, "[]", $"\"{LinePacketBuilder.Hash}\"", "null")).Build());
        var extended = await LineChecksTests.Run(kind, A.LinePacket(kind).With("index.json", LinePacketBuilder.Index(name, "[]", "null", $"\"{LinePacketBuilder.Hash}\"")).Build());

        Assert.Contains("has no acceptance.json", Assert.Single(Failures(set, $"{prefix}-006")).Message, StringComparison.Ordinal);
        Assert.Empty(Failures(extended, $"{prefix}-006"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_superseded_item_names_its_successor_and_only_a_superseded_item_does()
    {
        var kind = PacketKinds.Design;
        const string Old = "{ \"id\": \"AC-1\", \"text\": \"t\", \"status\": \"superseded\", \"supersededBy\": \"AC-2\", \"evidence\": [] }";
        const string New = "{ \"id\": \"AC-2\", \"text\": \"t2\", \"status\": \"failing\", \"evidence\": [] }";
        var good = await LineChecksTests.Run(kind, A.LinePacket(kind).With("acceptance.json", Acceptance($"{Old}, {New}")).Build());
        var nameless = await LineChecksTests.Run(kind, A.LinePacket(kind).With("acceptance.json", Acceptance($"{Old.Replace(", \"supersededBy\": \"AC-2\"", string.Empty, StringComparison.Ordinal)}, {New}")).Build());
        var stray = await LineChecksTests.Run(kind, A.LinePacket(kind).With("acceptance.json", Acceptance(Failing.Replace("\"evidence\"", "\"supersededBy\": \"AC-1\", \"evidence\"", StringComparison.Ordinal))).Build());
        var missing = await LineChecksTests.Run(kind, A.LinePacket(kind).With("acceptance.json", Acceptance(Old)).Build());

        Assert.Empty(Failures(good, "PKT-DSN-007"));
        Assert.Contains("names no successor", Assert.Single(Failures(nameless, "PKT-DSN-007")).Message, StringComparison.Ordinal);
        Assert.Contains("is not superseded", Assert.Single(Failures(stray, "PKT-DSN-007")).Message, StringComparison.Ordinal);
        Assert.Contains("not an item of this file", Assert.Single(Failures(missing, "PKT-DSN-007")).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_schema_error_names_the_file_and_the_field_not_the_clr_type()
    {
        var kind = PacketKinds.Design;

        var unknownField = await LineChecksTests.Run(kind, A.LinePacket(kind).With("index.json", LinePacketBuilder.Index("design", "[]", "null", "null").Replace("\"parents\"", "\"id\": \"x\", \"parents\"", StringComparison.Ordinal)).Build());
        var wrongShape = await LineChecksTests.Run(kind, A.LinePacket(kind).Edit("packet.json", "\"spec\": \"spec.md\"", "\"spec\": {}").Build());

        foreach (var message in new[] { Assert.Single(Failures(unknownField, "PKT-DSN-006")).Message, Assert.Single(Failures(wrongShape, "PKT-DSN-004")).Message })
        {
            Assert.DoesNotContain("PacketIndex", message, StringComparison.Ordinal);
            Assert.DoesNotContain("LinePacketManifest", message, StringComparison.Ordinal);
            Assert.DoesNotContain(".NET", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Aw.Packets", message, StringComparison.Ordinal);
        }

        Assert.StartsWith("index.json:", Failures(unknownField, "PKT-DSN-006").Single().Message, StringComparison.Ordinal);
        Assert.Contains("'id'", Failures(unknownField, "PKT-DSN-006").Single().Message, StringComparison.Ordinal);
        Assert.StartsWith("packet.json:", Failures(wrongShape, "PKT-DSN-004").Single().Message, StringComparison.Ordinal);
    }
}
