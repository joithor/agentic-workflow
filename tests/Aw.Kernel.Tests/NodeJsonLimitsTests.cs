using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;

namespace Aw.Kernel.Tests;

/// <summary>
/// What the Node JSON profile covers beyond strings, doubles and timestamps, and the limits it documents
/// (review of PR #97): untyped JSON, other numeric types, DateTime, required members, inheritance, keys.
/// </summary>
[Trait("Lane", "unit")]
public sealed class NodeJsonLimitsTests
{
    private static readonly JsonSerializerOptions _options = NodeJson.CreateOptions(TestJsonContext.Default);

    [Fact]
    public void Restringify_matches_JSON_stringify_of_JSON_parse() =>
        Assert.Equal(Text(Golden("untyped.compact.json")), Text(NodeJson.Restringify(Golden("untyped.input.json")).Value));

    [Fact]
    public void Restringify_indented_matches_JSON_stringify_with_two_space_indent() =>
        Assert.Equal(Text(Golden("untyped.indented.json")), Text(NodeJson.Restringify(Golden("untyped.input.json"), indented: true).Value));

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{} {}")]
    [InlineData("{\"a\":1,}")]
    [InlineData("[1,2")]
    [InlineData("'single'")]
    public void Restringify_of_text_JSON_parse_rejects_is_an_invalid_error(string json) =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Restringify(Encoding.UTF8.GetBytes(json)).Error.Code);

    [Fact]
    public void JsonElement_and_JsonNode_properties_are_written_like_JSON_stringify()
    {
        var input = Golden("untyped.input.json");
        using var document = JsonDocument.Parse(input);
        var wrapped = new Shapes.Wrapped("w", document.RootElement, JsonNode.Parse(Golden("untyped-paired.input.json"))!);

        Assert.Equal(Text(Golden("wrapped.compact.json")), Text(NodeJson.Serialize(wrapped, _options)));
    }

    [Fact]
    public void Limit_a_JsonNode_holding_a_lone_surrogate_fails_loudly()
    {
        using var document = JsonDocument.Parse("{}"u8.ToArray());
        var wrapped = new Shapes.Wrapped("w", document.RootElement, JsonNode.Parse(Golden("untyped.input.json"))!);

        _ = Assert.Throws<JsonException>(() => NodeJson.Serialize(wrapped, _options));
    }

    [Fact]
    public void DateTime_decimal_float_long_and_ulong_are_written_as_Node_holds_them()
    {
        var scalars = new Shapes.Scalars(
            When: new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc), Money: 1.10m, Whole: 1.0m, Big: 9007199254740993L,
            Single: 0.1f, Huge: 9223372036854775808UL, Nan: double.NaN, Maybe: null);

        Assert.Equal(Text(Golden("scalars.compact.json")), Text(NodeJson.Serialize(scalars, _options)));
    }

    [Fact]
    public void Scalars_read_back_and_write_the_same_bytes_and_null_reads_as_NaN()
    {
        var read = NodeJson.Deserialize<Shapes.Scalars>(Golden("scalars.compact.json"), _options).Value;

        Assert.Equal(Text(Golden("scalars.compact.json")), Text(NodeJson.Serialize(read, _options)));
        Assert.True(double.IsNaN(read.Nan));
        Assert.Null(read.Maybe);
        Assert.Equal(DateTimeKind.Utc, read.When.Kind);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void A_DateTime_that_is_not_UTC_is_a_bug(DateTimeKind kind) =>
        _ = Assert.Throws<InvalidOperationException>(() =>
            NodeJson.Serialize(new Shapes.When(new DateTime(2026, 10, 10, 6, 0, 0, kind)), _options));

    [Theory]
    [InlineData("{\"at\":\"2026-10-10T06:00:00Z\"}")]
    [InlineData("{\"at\":\"2026-10-10T06:00:00.000+00:00\"}")]
    public void A_DateTime_not_in_toISOString_form_is_an_invalid_error(string json) =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Deserialize<Shapes.When>(Encoding.UTF8.GetBytes(json), _options).Error.Code);

    [Theory]
    [InlineData("{\"money\":\"1\"}")]
    [InlineData("{\"when\":\"2026-10-10T06:00:00.000Z\",\"money\":1,\"whole\":1,\"big\":1.5,\"single\":0,\"huge\":0,\"nan\":0}")]
    [InlineData("{\"when\":\"2026-10-10T06:00:00.000Z\",\"money\":1,\"whole\":1,\"big\":1e+21,\"single\":0,\"huge\":0,\"nan\":0}")]
    [InlineData("{\"when\":\"2026-10-10T06:00:00.000Z\",\"money\":1,\"whole\":1,\"big\":1,\"single\":0,\"huge\":-1,\"nan\":0}")]
    public void A_number_the_target_type_cannot_hold_is_an_invalid_error(string json) =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Deserialize<Shapes.Scalars>(Encoding.UTF8.GetBytes(json), _options).Error.Code);

    [Fact]
    public void A_required_init_member_that_is_missing_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Deserialize<Shapes.RequiredInit>("{}"u8, _options).Error.Code);

    [Fact]
    public void A_null_for_a_non_nullable_init_member_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", NodeJson.Deserialize<Shapes.InitOnly>("{\"name\":null}"u8, _options).Error.Code);

    [Fact]
    public void Limit_a_missing_init_member_without_required_keeps_its_initializer() =>
        Assert.Null(NodeJson.Deserialize<Shapes.InitOnly>("{}"u8, _options).Value.Name);

    [Fact]
    public void Limit_a_null_collection_element_is_not_checked() =>
        Assert.Null(NodeJson.Deserialize<Shapes.Listed>("{\"items\":[\"a\",null]}"u8, _options).Value.Items[1]);

    [Fact]
    public void Limit_derived_members_come_before_base_members_unless_the_base_orders_itself()
    {
        Assert.Equal("{\"b\":2,\"a\":1}", Text(NodeJson.Serialize(new Shapes.DerivedShape(1, 2), _options)));
        Assert.Equal("{\"a\":1,\"b\":2}", Text(NodeJson.Serialize(new Shapes.OrderedDerived(2) { A = 1 }, _options)));
    }

    [Fact]
    public void A_lone_surrogate_in_a_dictionary_key_fails_loudly_instead_of_writing_a_replacement_character()
    {
        var keys = new Dictionary<string, int>(StringComparer.Ordinal) { ["lone " + (char)0xD800] = 1 };

        _ = Assert.Throws<JsonException>(() => NodeJson.Serialize(keys, _options));
    }

    private static byte[] Golden(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Golden", name));

    private static string Text(byte[] utf8) => Encoding.UTF8.GetString(utf8);
}
