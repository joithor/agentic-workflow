using Aw.Context.Core.Code;

namespace Aw.Context.Tests.Code;

public sealed class NameMatcherTests
{
    [Fact, Trait("Lane", "unit")]
    public void NameMatcher_path_terms_are_the_last_two_segments_in_slash_and_dotted_form() =>
        Assert.Equal(["sharing/links", "sharing.links"], NameMatcher.PathTerms("src/sharing/links.ts"), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void NameMatcher_path_terms_of_a_one_directory_file_use_its_directory() =>
        Assert.Equal(["src/app", "src.app"], NameMatcher.PathTerms("src/app.ts"), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void NameMatcher_path_terms_of_a_root_file_are_empty() =>
        Assert.Empty(NameMatcher.PathTerms("app.ts"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("import { x } from '../sharing/links';", "sharing/links")]
    [InlineData("import x from \"../../sharing/links.js\"", "sharing/links")]
    [InlineData("require(\"sharing.links\")", "sharing.links")]
    [InlineData("#include <sharing/links.h>", "sharing/links")]
    [InlineData("see Sharing/Links", "sharing/links")]
    public void NameMatcher_finds_a_path_with_dots_quotes_and_case(string text, string expected) =>
        Assert.Equal(expected, NameMatcher.FindPath(text, ["sharing/links", "sharing.links"]));

    [Theory, Trait("Lane", "unit")]
    [InlineData("from 'sharing/links-extra'")]
    [InlineData("from 'xsharing/links'")]
    [InlineData("from 'sharing/links2'")]
    [InlineData("from 'sharing/linksService'")]
    [InlineData("nothing here")]
    public void NameMatcher_rejects_a_longer_word_around_the_path(string text) =>
        Assert.Null(NameMatcher.FindPath(text, ["sharing/links", "sharing.links"]));

    [Theory, Trait("Lane", "unit")]
    [InlineData("SELECT * FROM USERS;")]
    [InlineData("select * from Users where")]
    public void NameMatcher_plural_endings_match_without_regard_to_case(string text) =>
        Assert.NotNull(NameMatcher.FindTerm(text, ["user", "user_"]));

    [Theory, Trait("Lane", "unit")]
    [InlineData("CREATE TABLE shared_links (", "shared_link")]
    [InlineData("ALTER TABLE shared_link ADD", "shared_link")]
    [InlineData("shared_link_id int", "shared_link")]
    [InlineData("indexes on shared_links_idx", "shared_link")]
    [InlineData("TABLE \"shared_link\"", "shared_link")]
    [InlineData("TABLE shared_Link", "shared_link")]
    public void NameMatcher_term_allows_a_plural_and_a_suffix_after_a_separator(string text, string expected) =>
        Assert.Equal(expected, NameMatcher.FindTerm(text, ["shared_link"]));

    [Theory, Trait("Lane", "unit")]
    [InlineData("reshared_link")]
    [InlineData("shared_linkage")]
    [InlineData("shared_linkx")]
    [InlineData("1shared_link")]
    [InlineData("shared_link2")]
    public void NameMatcher_term_rejects_a_longer_word_around_it(string text) =>
        Assert.Null(NameMatcher.FindTerm(text, ["shared_link"]));

    [Fact, Trait("Lane", "unit")]
    public void NameMatcher_term_reports_which_term_matched() =>
        Assert.Equal("shared link", NameMatcher.FindTerm("a shared link table", ["shared-link", "shared_link", "sharedlink", "shared link"]));
}
