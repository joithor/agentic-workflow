using Aw.Meta.Core.Github;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Github;

public sealed class GhJsonTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_view_reads_head_branch_title_draft_and_state()
    {
        var view = GhJson.View("""{"headRefOid":"abc123","headRefName":"feat/x","isDraft":true,"title":"Add x","state":"MERGED"}""");

        Assert.Equal(new PrDetails("abc123", "feat/x", IsDraft: true, "Add x", PrState.Merged), view);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_view_without_a_field_keeps_its_default() =>
        Assert.Equal(new PrDetails("abc", string.Empty, IsDraft: false, string.Empty, PrState.Open), GhJson.View("""{"headRefOid":"abc"}"""));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1]")]
    public void A_view_that_is_not_an_object_is_nothing(string json) => Assert.Null(GhJson.View(json));

    [Fact, Trait("Lane", "unit")]
    public void A_list_reads_every_pull_request_and_skips_what_has_no_number()
    {
        var list = GhJson.List(
            """[{"number":190,"state":"OPEN","isDraft":false,"headRefOid":"aaa","mergeable":"UNKNOWN","title":"t"},"""
            + """{"state":"OPEN"},"""
            + """{"number":191,"state":"CLOSED","isDraft":true,"headRefOid":"bbb","mergeable":"CONFLICTING"}]""");

        Assert.Equal([new PrView(190, PrState.Open, IsDraft: false, "aaa", "UNKNOWN"), new PrView(191, PrState.Closed, IsDraft: true, "bbb", "CONFLICTING")], list);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("oops")]
    public void A_list_that_is_not_an_array_is_nothing(string json) => Assert.Null(GhJson.List(json));

    [Theory, Trait("Lane", "unit")]
    [InlineData("0123456789abcdef\trefs/heads/main\n", "0123456789abcdef")]
    [InlineData("\n", null)]
    [InlineData("", null)]
    public void The_remote_sha_is_the_first_word(string output, string? sha) => Assert.Equal(sha, GhJson.RemoteSha(output));
}
