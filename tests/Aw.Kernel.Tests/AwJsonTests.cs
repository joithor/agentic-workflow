using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Kernel.Tests;

/// <summary>The AwJson profile's own behaviour: names, escaping, timestamps, strict reading, layout.</summary>
[Trait("Lane", "unit")]
public sealed class AwJsonTests
{
    private const string ProfileJson =
        "{\"name\":\"café <b>&'\",\"count\":3,\"ratio\":0.5,\"money\":1.10,\"big\":9007199254740993,"
        + "\"at\":\"2026-10-10T06:00:00.0070000Z\",\"day\":\"2026-10-10T06:00:00.0000000Z\",\"note\":null,"
        + "\"tags\":[\"a\"],\"counts\":{\"Key\":1}}";

    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(TestJsonContext.Default);

    private static readonly Shapes.Profile _profile = new(
        Name: "café <b>&'",
        Count: 3,
        Ratio: 0.5,
        Money: 1.10m,
        Big: 9007199254740993L,
        At: new DateTimeOffset(2026, 10, 10, 8, 0, 0, 7, TimeSpan.FromHours(2)),
        Day: new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc),
        Note: null,
        Tags: ["a"],
        Counts: new Dictionary<string, int>(StringComparer.Ordinal) { ["Key"] = 1 });

    [Fact]
    public void Writes_camel_case_names_readable_text_exact_numbers_nulls_and_UTC_timestamps() =>
        Assert.Equal(ProfileJson, Text(AwJson.Serialize(_profile, _options)));

    [Fact]
    public void Reading_and_writing_again_gives_the_same_bytes_and_the_same_instant()
    {
        var read = AwJson.Deserialize<Shapes.Profile>(Encoding.UTF8.GetBytes(ProfileJson), _options).Value;

        Assert.Equal(ProfileJson, Text(AwJson.Serialize(read, _options)));
        Assert.Equal(_profile.At, read.At);
        Assert.Equal(TimeSpan.Zero, read.At.Offset);
        Assert.Equal(DateTimeKind.Utc, read.Day.Kind);
    }

    [Fact]
    public void Indented_output_uses_two_spaces_and_LF_on_every_OS()
    {
        var text = Text(AwJson.Serialize(new Stamp(_profile.At), AwJson.CreateOptions(TestJsonContext.Default, indented: true)));

        Assert.Equal("{\n  \"at\": \"2026-10-10T06:00:00.0070000Z\"\n}", text);
    }

    [Fact]
    public void A_timestamp_with_an_offset_reads_as_the_same_instant_in_UTC()
    {
        var read = AwJson.Deserialize<Stamp>("{\"at\":\"2026-10-10T08:00:00.007+02:00\"}"u8, _options).Value;

        Assert.Equal((new DateTimeOffset(2026, 10, 10, 6, 0, 0, 7, TimeSpan.Zero), TimeSpan.Zero), (read.At, read.At.Offset));
    }

    [Theory]
    [InlineData("{\"at\":\"2026-10-10T06:37:59\"}")]
    [InlineData("{\"at\":\"10/10/2026 06:37:59 +00:00\"}")]
    [InlineData("{\"at\":\"not a time\"}")]
    [InlineData("{\"at\":42}")]
    [InlineData("{\"at\":")]
    public void A_timestamp_without_an_offset_or_not_ISO_8601_or_broken_JSON_is_an_invalid_error(string json) =>
        Assert.Equal("AW-KERNEL-001", AwJson.Deserialize<Stamp>(Encoding.UTF8.GetBytes(json), _options).Error.Code);

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void Writing_a_DateTime_that_is_not_UTC_is_a_bug(DateTimeKind kind) =>
        _ = Assert.Throws<InvalidOperationException>(() =>
            AwJson.Serialize(new Shapes.When(new DateTime(2026, 10, 10, 6, 0, 0, kind)), _options));

    [Fact]
    public void A_JSON_null_document_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", AwJson.Deserialize<Stamp>("null"u8, _options).Error.Code);

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"note\":\"n\"}")]
    [InlineData("{\"name\":null}")]
    public void A_missing_positional_parameter_or_a_null_for_a_non_nullable_one_is_an_invalid_error(string json) =>
        Assert.Equal("AW-KERNEL-001", AwJson.Deserialize<Named>(Encoding.UTF8.GetBytes(json), _options).Error.Code);

    [Fact]
    public void An_optional_parameter_may_be_missing_and_a_nullable_one_may_be_null()
    {
        Assert.Equal(new Named("a"), AwJson.Deserialize<Named>("{\"name\":\"a\"}"u8, _options).Value);
        Assert.Equal(new Named("a"), AwJson.Deserialize<Named>("{\"name\":\"a\",\"note\":null}"u8, _options).Value);
    }

    [Fact]
    public void A_required_init_member_that_is_missing_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", AwJson.Deserialize<Shapes.RequiredInit>("{}"u8, _options).Error.Code);

    [Fact]
    public void A_null_for_a_non_nullable_init_member_is_an_invalid_error() =>
        Assert.Equal("AW-KERNEL-001", AwJson.Deserialize<Shapes.InitOnly>("{\"name\":null}"u8, _options).Error.Code);

    [Fact]
    public void Limit_a_missing_init_member_without_required_keeps_its_initializer() =>
        Assert.Null(AwJson.Deserialize<Shapes.InitOnly>("{}"u8, _options).Value.Name);

    [Fact]
    public void Limit_a_null_collection_element_is_not_checked() =>
        Assert.Null(AwJson.Deserialize<Shapes.Listed>("{\"items\":[\"a\",null]}"u8, _options).Value.Items[1]);

    private static string Text(byte[] utf8) => Encoding.UTF8.GetString(utf8);
}
