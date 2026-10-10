using Aw.Judge.Core.Json;

namespace Aw.Judge.Tests.Json;

// Expected strings recorded from node (JSON.parse / JSON.stringify / Number), the TS judge's runtime.
public sealed class JsJsonTests
{
    [Fact, Trait("Lane", "unit")]
    public void Stringify_escapes_a_string_as_JSON_stringify_does()
    {
        var text = "a\"b\\c\n\t\r\b\f\u0001\u001f\u007f é 😀 \ud800 \u2028/<>&'";

        var json = JsJson.Stringify(JsValue.From(text));

        Assert.Equal("\"a\\\"b\\\\c\\n\\t\\r\\b\\f\\u0001\\u001f\u007f é 😀 \\ud800 \u2028/<>&'\"", json);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0.85, "0.85")]
    [InlineData(1d, "1")]
    [InlineData(-0d, "0")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(123456789012345680000d, "123456789012345680000")]
    [InlineData(0.30000000000000004, "0.30000000000000004")]
    [InlineData(1.5e-10, "1.5e-10")]
    [InlineData(100d, "100")]
    [InlineData(1e300, "1e+300")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(-1.25, "-1.25")]
    [InlineData(0.7, "0.7")]
    public void Stringify_writes_a_number_as_JavaScript_does(double value, string expected) =>
        Assert.Equal(expected, JsJson.Stringify(JsValue.From(value)));

    [Fact, Trait("Lane", "unit")]
    public void A_non_finite_number_is_written_as_null() =>
        Assert.Equal("[null,null]", JsJson.Stringify(new JsArray([JsValue.From(double.NaN), JsValue.From(double.PositiveInfinity)])));

    [Fact, Trait("Lane", "unit")]
    public void Parse_orders_keys_as_JavaScript_and_keeps_a_duplicate_key_in_its_first_place()
    {
        var value = JsJson.Parse("""{"b":1,"2":2,"a":3,"1":4,"b":5,"01":6,"4294967295":7,"4294967294":8}""");

        Assert.NotNull(value);
        Assert.Equal("""{"1":4,"2":2,"4294967294":8,"b":5,"a":3,"01":6,"4294967295":7}""", JsJson.Stringify(value));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{")]
    [InlineData("[1,]")]
    [InlineData("\uFEFF{}")]
    [InlineData("01")]
    [InlineData("1 2")]
    [InlineData("{'a':1}")]
    [InlineData("NaN")]
    [InlineData("{\"a\":1}/* c */")]
    public void Parse_refuses_what_JSON_parse_refuses(string text) => Assert.Null(JsJson.Parse(text));

    [Theory, Trait("Lane", "unit")]
    [InlineData("1", "1")]
    [InlineData("\"x\"", "\"x\"")]
    [InlineData("null", "null")]
    [InlineData("true", "true")]
    [InlineData(" [ ] ", "[]")]
    [InlineData("\"\\ud800\"", "\"\\ud800\"")]
    [InlineData("-0", "0")]
    [InlineData("1e400", "null")]
    [InlineData("\"\\u00e9\\/\"", "\"é/\"")]
    [InlineData("{\"a\":{\"a\":1},\"a\":[2]}", "{\"a\":[2]}")]
    public void Parse_then_stringify_matches_node(string text, string expected)
    {
        var value = JsJson.Parse(text);

        Assert.NotNull(value);
        Assert.Equal(expected, JsJson.Stringify(value));
    }

    [Fact, Trait("Lane", "unit")]
    public void StringifyIndented_matches_JSON_stringify_with_two_spaces()
    {
        var value = JsJson.Parse("""{"q":{"a":[1,2],"e":{},"f":[],"s":"x","n":null,"b":true}}""");

        Assert.NotNull(value);
        Assert.Equal(
            "{\n  \"q\": {\n    \"a\": [\n      1,\n      2\n    ],\n    \"e\": {},\n    \"f\": [],\n    \"s\": \"x\",\n    \"n\": null,\n    \"b\": true\n  }\n}",
            JsJson.StringifyIndented(value));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "0")]
    [InlineData(" 0.5 ", "0.5")]
    [InlineData("0x10", "16")]
    [InlineData("1e400", "Infinity")]
    [InlineData("abc", "NaN")]
    [InlineData("1_0", "NaN")]
    [InlineData("+.5", "0.5")]
    [InlineData("5.", "5")]
    [InlineData(".", "NaN")]
    [InlineData("-0x10", "NaN")]
    [InlineData("Infinity", "Infinity")]
    [InlineData("-Infinity", "-Infinity")]
    [InlineData("\u00a00.25\n", "0.25")]
    [InlineData("0b101", "5")]
    [InlineData("0o17", "15")]
    [InlineData("1e", "NaN")]
    [InlineData("  ", "0")]
    [InlineData("0X1f", "31")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("00.5", "0.5")]
    [InlineData("\u20280.5", "0.5")]
    [InlineData(".5e1", "5")]
    public void FromString_reads_text_as_Number_does(string text, string expected) =>
        Assert.Equal(expected, JsNumbers.Format(JsNumbers.FromString(text)));

    [Fact, Trait("Lane", "unit")]
    public void Spread_copies_a_string_by_UTF16_code_unit_and_an_array_by_index()
    {
        var text = JsObject.Spread(JsValue.From("a😀"));
        var array = JsObject.Spread(new JsArray([JsValue.From(1), JsValue.From("x")])).Set("enabled", JsValue.From(value: true));

        Assert.Equal("{\"0\":\"a\",\"1\":\"\\ud83d\",\"2\":\"\\ude00\"}", JsJson.Stringify(text));
        Assert.Equal("{\"0\":1,\"1\":\"x\",\"enabled\":true}", JsJson.Stringify(array));
        Assert.Equal(0, JsObject.Spread(JsValue.From(3)).Count);
    }
}
