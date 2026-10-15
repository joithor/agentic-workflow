using System.Text.Json;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class JsonPointerTests
{
    // The example document of RFC 6901 section 5.
    private static readonly JsonElement _rfc = JsonDocument.Parse(
        """{"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\"l":6," ":7,"m~n":8}""").RootElement;

    [Theory, Trait("Lane", "unit")]
    [InlineData("/foo/0", "\"bar\"")]
    [InlineData("/foo/1", "\"baz\"")]
    [InlineData("/", "0")]
    [InlineData("/a~1b", "1")]
    [InlineData("/c%d", "2")]
    [InlineData("/e^f", "3")]
    [InlineData("/g|h", "4")]
    [InlineData("/i\\j", "5")]
    [InlineData("/k\"l", "6")]
    [InlineData("/ ", "7")]
    [InlineData("/m~0n", "8")]
    public void JsonPointer_resolves_rfc6901_escapes(string reference, string expectedJson)
    {
        var found = JsonPointer.Resolve(_rfc, reference);

        Assert.Equal(expectedJson, found?.GetRawText());
    }

    [Fact, Trait("Lane", "unit")]
    public void JsonPointer_resolves_the_empty_pointer_to_the_whole_document() =>
        Assert.Equal(JsonValueKind.Object, JsonPointer.Resolve(_rfc, "")?.ValueKind);

    [Theory, Trait("Lane", "unit")]
    [InlineData("/nope")]
    [InlineData("/foo/2")]
    [InlineData("/foo/01")]
    [InlineData("/foo/-")]
    [InlineData("/foo/x")]
    [InlineData("/foo/0/deeper")]
    public void JsonPointer_does_not_resolve_a_missing_member_a_bad_index_or_a_dash(string reference) =>
        Assert.Null(JsonPointer.Resolve(_rfc, reference));

    [Theory, Trait("Lane", "unit")]
    [InlineData("a/b")]
    [InlineData("/a~2b")]
    [InlineData("/a~")]
    [InlineData("#/a")]
    public void JsonPointer_names_the_problem_with_an_unknown_syntax(string reference) =>
        Assert.NotNull(JsonPointer.Problem(reference));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/a~0~1b")]
    [InlineData("/items/0/title")]
    public void JsonPointer_accepts_a_well_formed_pointer(string reference) =>
        Assert.Null(JsonPointer.Problem(reference));
}
