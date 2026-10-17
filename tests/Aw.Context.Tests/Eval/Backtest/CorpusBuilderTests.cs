using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class CorpusBuilderTests
{
    private static readonly string[] _teams = ["SPL"];

    private static Corpus Build(RepoHistory history, params BriefIssue[] issues) => CorpusBuilder.Build(issues, [history], _teams, _ => false);

    [Fact, Trait("Lane", "unit")]
    public void Sample_cut_is_last_first_parent_commit_at_or_before_issue_creation()
    {
        var history = Builders.History(Builders.Commit("c1", day: 1), Builders.Commit("c2", day: 5), Builders.Commit("c3", day: 9),
            Builders.MergeFrom("m1", 12, "Merge PR #1: SPL-101 split panes", "feat/spl-101", "src/a.ts"));

        var corpus = Build(history, Builders.Issue("SPL-101", createdDay: 5, updatedDay: 8));

        var sample = Assert.Single(corpus.Samples);
        Assert.Equal(("c2", "m1"), (sample.Cut, sample.Merge));   // c2 is dated exactly at the creation time: at-or-before
        Assert.True(sample.Leaky);
        Assert.Equal(["src/a.ts"], sample.TruthFiles);
    }

    [Fact, Trait("Lane", "unit")]
    public void Issue_without_merged_pr_is_left_out_and_counted()
    {
        var history = Builders.History(Builders.Commit("c1", day: 1), Builders.Merge("m1", day: 12, "Merge PR #1: SPL-101", "src/a.ts"));

        var corpus = Build(history, Builders.Issue("SPL-101", createdDay: 2, updatedDay: 3), Builders.Issue("SPL-106", createdDay: 2, updatedDay: 3));

        Assert.Equal(["SPL-101"], corpus.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal((1, 0, 0), (corpus.LeftOut.NoMerge, corpus.LeftOut.OutsideTeams, corpus.LeftOut.ExtractFailed));
        Assert.Equal(["SPL-106"], corpus.LeftOut.Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void Merge_title_or_branch_key_outside_profile_teams_is_not_a_sample()
    {
        var history = Builders.History(Builders.Commit("c1", day: 1), Builders.MergeFrom("m1", 12, "Merge PR #1: OPS-7 rotate", "feat/ops-7", "src/a.ts"),
            Builders.MergeFrom("m2", 13, "Merge PR #2", "SPL-102-tabs", "src/b.ts"));

        var corpus = Build(history, Builders.Issue("OPS-7", createdDay: 2, updatedDay: 3), Builders.Issue("SPL-102", createdDay: 2, updatedDay: 3), Builders.Issue("SPL-10", createdDay: 2, updatedDay: 3));

        Assert.Equal(["SPL-102"], corpus.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);   // the branch carries the key; SPL-10 is not a prefix hit of SPL-102
        Assert.Equal(1, corpus.LeftOut.OutsideTeams);
        Assert.Equal(1, corpus.LeftOut.NoMerge);
    }

    [Fact, Trait("Lane", "unit")]
    public void Truth_files_drop_deny_globs_and_a_merge_with_no_truth_left_is_left_out()
    {
        var history = Builders.History(Builders.Commit("c1", day: 1), Builders.Merge("m1", day: 12, "SPL-101", "src/a.ts", "secrets/key.txt"), Builders.Merge("m2", day: 13, "SPL-102", "secrets/other.txt"));

        var corpus = CorpusBuilder.Build([Builders.Issue("SPL-101", createdDay: 2, updatedDay: 3), Builders.Issue("SPL-102", createdDay: 2, updatedDay: 3)], [history], _teams, f => f.StartsWith("secrets/", StringComparison.Ordinal));

        Assert.Equal(["src/a.ts"], Assert.Single(corpus.Samples).TruthFiles);
        Assert.Equal(1, corpus.LeftOut.NoMerge);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(3)]   // merged two days before the issue existed
    [InlineData(5)]   // merged at the very moment the issue was created
    public void A_merge_at_or_before_the_issue_existed_is_left_out_never_scored_as_clean(int mergeDay)
    {
        var history = Builders.History(Builders.Commit("c1", day: 1), Builders.Merge("m1", mergeDay, "Merge PR #1: SPL-101", "src/a.ts"), Builders.Commit("c2", day: 6));

        var corpus = Build(history, Builders.Issue("SPL-101", createdDay: 5, updatedDay: 8));

        Assert.Empty(corpus.Samples);   // the cut would already hold the answer
        Assert.Equal(1, corpus.LeftOut.NoMerge);
        Assert.Equal(["SPL-101"], corpus.LeftOut.Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_issue_created_before_the_first_commit_has_no_cut_and_is_left_out()
    {
        var history = Builders.History(Builders.Commit("c1", day: 5), Builders.Merge("m1", day: 12, "SPL-101", "src/a.ts"));

        var corpus = Build(history, Builders.Issue("SPL-101", createdDay: 2, updatedDay: 3));

        Assert.Empty(corpus.Samples);
        Assert.Equal(1, corpus.LeftOut.NoMerge);
    }
}
