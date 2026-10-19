using Aw.Context.Core.Code;

namespace Aw.Context.Tests.Code;

public sealed class CodeTermsTests
{
    [Fact, Trait("Lane", "unit")]
    public void TermForms_kebab_snake_joined_and_spaced_for_a_kebab_name() =>
        Assert.Equal(["shared-link", "shared_link", "sharedlink", "shared link"], CodeTerms.Forms(["shared-link"]), StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SharedLink")]
    [InlineData("sharedLink")]
    [InlineData("Shared Link")]
    [InlineData("shared.link")]
    public void TermForms_split_camel_pascal_and_separated_names_the_same_way(string name) =>
        Assert.Equal(["shared-link", "shared_link", "sharedlink", "shared link"], CodeTerms.Forms([name]), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void TermForms_split_an_acronym_before_a_word() =>
        Assert.Equal(["xml-parser", "xml_parser", "xmlparser", "xml parser"], CodeTerms.Forms(["XMLParser"]), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void TermForms_include_the_alias_terms_once_each() =>
        Assert.Equal(["shared-link", "shared_link", "sharedlink", "shared link", "short-url", "short_url", "shorturl", "short url"],
            CodeTerms.Forms(["shared-link", "Shared Link", "short url"]), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void TermForms_under_4_characters_are_not_searchable()
    {
        Assert.Empty(CodeTerms.Forms(["ab", "a-b", "tag"]));
        Assert.Equal(["note"], CodeTerms.Forms(["note"]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void TermForms_keep_only_the_forms_that_are_long_enough() =>
        Assert.Equal(["a-bc", "a_bc", "a bc"], CodeTerms.Forms(["a-bc"]), StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("thing:shared-link", "shared-link")]
    [InlineData("feature:a:b", "a:b")]
    [InlineData("plain", "plain")]
    public void KeyName_is_the_part_after_the_first_colon(string key, string expected) =>
        Assert.Equal(expected, CodeTerms.KeyName(key));
}
