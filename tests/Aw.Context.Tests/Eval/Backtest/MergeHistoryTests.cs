using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class MergeHistoryTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task MergeHistory_lists_the_five_fixture_merges_with_keys_and_changed_files()
    {
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), fixture.Path);

        var history = (await new GitMergeHistory(new GitCli()).History(repo, TestContext.Current.CancellationToken)).Value;

        var merges = history.Commits.Where(c => c.IsMerge).ToList();
        Assert.Equal(fixture.PullRequests.Select(p => p.MergeCommit), merges.Select(m => m.Sha), StringComparer.Ordinal);
        Assert.All(fixture.PullRequests.Zip(merges), pair =>
        {
            Assert.Contains(pair.First.IssueKey, pair.Second.Subject + " " + pair.Second.Branch, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(pair.First.Files.Order(StringComparer.Ordinal), pair.Second.ChangedFiles, StringComparer.Ordinal);   // truth matches the README table
        });
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero), history.Commits[0].At);
        Assert.False(history.Commits[0].IsMerge);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task MergeHistory_reads_first_parent_only()
    {
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), fixture.Path);

        var history = (await new GitMergeHistory(new GitCli()).History(repo, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(6, history.Commits.Count);   // the initial commit and five merges: the feature-branch commits are not on the first parent chain
        Assert.DoesNotContain(history.Commits, c => c.Subject.StartsWith("SPL-1", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task MergeHistory_of_a_missing_checkout_is_048()
    {
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), Path.Combine(Path.GetTempPath(), "aw-no-such-repo-" + RealTime.NewGuid().ToString("N")));

        var history = await new GitMergeHistory(new GitCli()).History(repo, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, history.Error.Code);
    }
}
