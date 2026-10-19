using Aw.Context.Core.History;

namespace Aw.Context.Tests.History;

public sealed class MergeSubjectTests
{
    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_github_merge_gives_pr_and_branch_without_owner() =>
        Assert.Equal(new MergeSubject(Pr: 5, Branch: "nts-130-link-revocation"), MergeSubject.Parse("Merge pull request #5 from fixture-labs/nts-130-link-revocation"));

    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_squash_suffix_gives_pr() =>
        Assert.Equal(new MergeSubject(Pr: 6, Branch: null), MergeSubject.Parse("NTS-150: x (#6)"));

    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_bitbucket_form()
    {
        Assert.Equal(new MergeSubject(Pr: 12, Branch: null), MergeSubject.Parse("Add tabs (pull request #12)"));
        Assert.Equal(new MergeSubject(Pr: 12, Branch: "feature/tabs"), MergeSubject.Parse("Merged in feature/tabs (pull request #12)"));
    }

    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_merge_branch_gives_branch_only() =>
        Assert.Equal(new MergeSubject(Pr: null, Branch: "release-2"), MergeSubject.Parse("Merge branch 'release-2' into main"));

    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_hash_inside_text_is_not_a_pr()
    {
        Assert.Equal(new MergeSubject(Pr: null, Branch: null), MergeSubject.Parse("Fix #12 in the share dialog"));
        Assert.Equal(new MergeSubject(Pr: null, Branch: null), MergeSubject.Parse("Revert (#7) of the thing"));
        Assert.Equal(new MergeSubject(Pr: null, Branch: null), MergeSubject.Parse(""));
    }

    [Fact, Trait("Lane", "unit")]
    public void MergeSubject_digits_over_nine_are_not_a_pr()
    {
        Assert.Equal(new MergeSubject(Pr: 999999999, Branch: null), MergeSubject.Parse("x (#999999999)"));
        Assert.Equal(new MergeSubject(Pr: null, Branch: null), MergeSubject.Parse("x (#1000000000)"));
    }
}
