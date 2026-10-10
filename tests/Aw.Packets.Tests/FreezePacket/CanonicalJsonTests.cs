using System.Text;
using System.Text.Json.Nodes;
using Aw.Packets.Core.Digest;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.FreezePacket;

public sealed class CanonicalJsonTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"b\":1,\"a\":[{\"d\":null,\"c\":\"é\"}]}", "{\"a\":[{\"c\":\"é\",\"d\":null}],\"b\":1}")]
    [InlineData("{ \"B\" : true, \"a\" : \"<&>\", \"_\": 10 }", "{\"B\":true,\"_\":10,\"a\":\"<&>\"}")]
    [InlineData("[ \"line\\nbreak\", \"quote\\\"\" ]", "[\"line\\nbreak\",\"quote\\\"\"]")]
    public void Canonical_json_sorts_keys_by_ordinal_drops_whitespace_and_escapes_only_what_json_requires(string input, string expected) =>
        Assert.Equal(expected, Encoding.UTF8.GetString(PacketDigest.Canonical(JsonNode.Parse(input)!)));

    [Fact, Trait("Lane", "unit")]
    public void Canonical_json_writes_non_bmp_characters_as_surrogate_escapes_and_numbers_as_written() =>
        Assert.Equal("{\"e\":\"\\uD83D\\uDE00 ü \\u2028\",\"n\":1.50}", Encoding.UTF8.GetString(PacketDigest.Canonical(JsonNode.Parse("{\"n\": 1.50, \"e\": \"\U0001F600 ü \u2028\"}")!)));

    [Fact, Trait("Lane", "unit")]
    public void The_digest_of_a_known_packet_is_pinned()
    {
        var files = PacketFiles.Of(A.Packet().Build()).ToDictionary(static f => f.Key, static f => new ReadOnlyMemory<byte>(f.Value), StringComparer.Ordinal);
        var loaded = new Aw.Packets.Core.Model.LoadedPacket(A.Packet().Build(), files);

        // digestVersion 1: a change to these bytes needs a new PacketDigest.Version, not a new pin.
        Assert.Equal("sha256:f840e32a5a4fcec56de95512e458d1efd8e7a8bfeea92ce9d2f79a094190c177", PacketDigest.Compute(loaded).Value.Value);
    }
}
