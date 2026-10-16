using Aw.Meta.Core.Github;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Github;

public sealed class GhJsonBranchesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Pull_requests_are_read_with_their_head_branches()
    {
        const string json = """[{"number":7,"state":"MERGED","headRefName":"feat/x","headRefOid":"abc1234"},{"number":8,"state":"OPEN","headRefName":"fix/y","headRefOid":"def5678"},{"number":9,"state":"CLOSED","headRefName":"z","headRefOid":"0000000"}]""";

        var list = GhJson.Branches(json);

        Assert.Equal(
            [new PrBranch(7, PrState.Merged, "feat/x", "abc1234"), new PrBranch(8, PrState.Open, "fix/y", "def5678"), new PrBranch(9, PrState.Closed, "z", "0000000")],
            list);
    }

    [Fact, Trait("Lane", "unit")]
    public void Anything_that_is_not_a_list_is_nothing_and_entries_without_a_number_are_skipped()
    {
        Assert.Null(GhJson.Branches("{}"));
        Assert.Null(GhJson.Branches("not json"));
        Assert.Empty(GhJson.Branches("""[{"state":"OPEN"}]""")!);
    }
}
