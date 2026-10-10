using System.Text;
using Aw.Kernel.Json;

namespace Aw.Kernel.Tests;

// Byte compatibility with what the TS packages write today (agent-readiness spec \u00a74c, A1). The goldens
// are Node's own JSON.stringify output: Golden/make-goldens.mjs regenerates them.
[Trait("Lane", "unit")]
public sealed class NodeJsonTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Compact = NodeJson.CreateOptions(TestJsonContext.Default);
    private static readonly System.Text.Json.JsonSerializerOptions Indented = NodeJson.CreateOptions(TestJsonContext.Default, indented: true);

    [Fact]
    public void Writes_the_same_bytes_as_JSON_stringify() =>
        AssertSameBytes(Golden("fixture.compact.json"), NodeJson.Serialize(NodeJsonFixture.Value, Compact));

    [Fact]
    public void Writes_the_same_bytes_as_JSON_stringify_with_two_space_indent() =>
        AssertSameBytes(Golden("fixture.indented.json"), NodeJson.Serialize(NodeJsonFixture.Value, Indented));

    [Theory]
    [InlineData("fixture.compact.json", false)]
    [InlineData("fixture.indented.json", true)]
    public void Reading_a_Node_file_and_writing_it_back_is_byte_identical(string golden, bool indented)
    {
        var options = indented ? Indented : Compact;
        var read = NodeJson.Deserialize<NodeJsonFixture>(Golden(golden), options).Value;

        AssertSameBytes(Golden(golden), NodeJson.Serialize(read, options));
        Assert.Equal("lone \ud800 high", read.Strings[5]);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 1, 4, 5, 600, TimeSpan.Zero), read.UpdatedAt);
    }

    [Theory]
    [InlineData("{\"at\":\"2026-10-10T06:37:59Z\"}")]
    [InlineData("{\"at\":\"2026-10-10T06:37:59.0071Z\"}")]
    [InlineData("{\"at\":\"2026-10-10T08:37:59.007+02:00\"}")]
    [InlineData("{\"at\":\"not a time\"}")]
    [InlineData("{\"at\":")]
    public void A_timestamp_not_in_toISOString_form_or_broken_JSON_is_an_invalid_error(string json)
    {
        var result = NodeJson.Deserialize<Stamp>(Encoding.UTF8.GetBytes(json), Compact);

        Assert.Equal("AW-KERNEL-001", result.Error.Code);
    }

    [Fact]
    public void A_JSON_null_document_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Deserialize<Stamp>("null"u8, Compact).Error.Code);

    [Fact]
    public void NaN_and_infinities_are_written_as_null_like_JSON_stringify() =>
        Assert.Equal("[null,null,null]", Encoding.UTF8.GetString(
            NodeJson.Serialize(new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }, Compact)));

    private static byte[] Golden(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Golden", name));

    private static void AssertSameBytes(byte[] expected, byte[] actual) =>
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(actual));
}
