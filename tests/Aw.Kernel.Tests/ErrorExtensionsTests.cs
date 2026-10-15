using System.Text.Json;

namespace Aw.Kernel.Tests;

[Trait("Lane", "unit")]
public sealed class ErrorExtensionsTests
{
    private static readonly Error _base = KernelErrors.NotFound.With("no such anchor 'intro'");

    [Fact]
    public void An_error_has_no_extensions_until_one_is_attached()
    {
        Assert.Null(_base.Extensions);
        Assert.Equal(new Error("AW-KERNEL-002", ErrorKind.NotFound, "no such anchor 'intro'"), _base);
    }

    [Fact]
    public void Extensions_keep_the_order_they_were_attached_in_and_the_original_error_is_untouched()
    {
        var extended = _base
            .WithExtension("suggestedAnchors", Element("""[{"key":"a","label":"A"}]"""))
            .WithExtension("candidateCount", Element("1"));

        Assert.Equal("suggestedAnchors,candidateCount", string.Join(',', extended.Extensions!.Select(m => m.Key)));
        Assert.Equal("A", extended.Extensions![0].Value[0].GetProperty("label").GetString());
        Assert.Null(_base.Extensions);
        Assert.Equal((_base.Code, _base.Kind, _base.Message), (extended.Code, extended.Kind, extended.Message));
    }

    [Fact]
    public void A_value_is_serialized_through_the_callers_type_info_without_reflection()
    {
        var extended = _base.WithExtension<Anchor[]>("suggestedAnchors", [new Anchor("a", "A")], TestJsonContext.Default.AnchorArray);

        Assert.Equal("""[{"key":"a","label":"A"}]""", extended.Extensions![0].Value.GetRawText());
    }

    [Fact]
    public void A_value_survives_its_document_being_disposed()
    {
        using var document = JsonDocument.Parse("""{"k":"v"}""");
        var extended = _base.WithExtension("detailMap", document.RootElement);

        document.Dispose();

        Assert.Equal("v", extended.Extensions![0].Value.GetProperty("k").GetString());
    }

    [Theory]
    [InlineData("type")]
    [InlineData("title")]
    [InlineData("status")]
    [InlineData("detail")]
    [InlineData("instance")]
    [InlineData("traceId")]
    [InlineData("")]
    [InlineData("Suggested")]
    [InlineData("suggested-anchors")]
    [InlineData("suggested_anchors")]
    [InlineData("9lives")]
    [InlineData("naïve")]
    public void A_reserved_or_non_camelCase_name_is_refused(string name)
    {
        Assert.False(ErrorExtensions.IsValidName(name));
        _ = Assert.Throws<ArgumentException>(() => _base.WithExtension(name, Element("1")));
    }

    [Fact]
    public void A_name_is_valid_when_camelCase_and_at_most_64_characters()
    {
        Assert.True(ErrorExtensions.IsValidName("a"));
        Assert.True(ErrorExtensions.IsValidName("suggestedAnchors2"));
        Assert.True(ErrorExtensions.IsValidName(new string('a', ErrorExtensions.MaxNameLength)));
        Assert.False(ErrorExtensions.IsValidName(new string('a', ErrorExtensions.MaxNameLength + 1)));
        Assert.False(ErrorExtensions.IsValidName(null));
    }

    [Fact]
    public void Setting_a_name_twice_is_refused()
    {
        var extended = _base.WithExtension("candidates", Element("[]"));

        _ = Assert.Throws<ArgumentException>(() => extended.WithExtension("candidates", Element("[1]")));
    }

    [Fact]
    public void Extensions_are_not_part_of_the_errors_JSON()
    {
        var plain = JsonSerializer.Serialize(_base, TestJsonContext.Default.Error);
        var extended = JsonSerializer.Serialize(_base.WithExtension("candidates", Element("[]")), TestJsonContext.Default.Error);

        Assert.Equal(plain, extended);
        Assert.DoesNotContain("xtension", plain, StringComparison.Ordinal);
    }

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    internal sealed record Anchor(string Key, string Label);
}
