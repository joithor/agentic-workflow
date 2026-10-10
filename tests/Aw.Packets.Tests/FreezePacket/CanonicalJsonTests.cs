using System.Text;
using System.Text.Json.Nodes;
using Aw.Packets.Core.Digest;

namespace Aw.Packets.Tests.FreezePacket;

public sealed class CanonicalJsonTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"b\":1,\"a\":[{\"d\":null,\"c\":\"é\"}]}", "{\"a\":[{\"c\":\"é\",\"d\":null}],\"b\":1}")]
    [InlineData("{ \"B\" : true, \"a\" : \"<&>\", \"_\": 10 }", "{\"B\":true,\"_\":10,\"a\":\"<&>\"}")]
    [InlineData("[ \"line\\nbreak\", \"quote\\\"\" ]", "[\"line\\nbreak\",\"quote\\\"\"]")]
    public void Canonical_json_sorts_keys_by_ordinal_drops_whitespace_and_escapes_only_what_json_requires(string input, string expected) =>
        Assert.Equal(expected, Encoding.UTF8.GetString(PacketDigest.Canonical(JsonNode.Parse(input)!)));
}
