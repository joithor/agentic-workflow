using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class AsOfRepoDocsTests
{
    private static readonly string[] _globs = ["docs/**/*.md", "**/PRD*.md", "**/TDD*.md"];

    private static SourceQuery Query(params string[] terms) => new(terms, AsOf: null, Limit: 20, TimeSpan.FromSeconds(5));

    [Fact, Trait("Lane", "integration")]
    public async Task AsOfRepoDocs_at_cut_of_SPL_102_has_no_decision_doc_0001()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), fixture.Path);
        var initial = (await new GitMergeHistory(new GitCli()).History(repo, TestContext.Current.CancellationToken)).Value.Commits[0].Sha;

        var atCut = (await AsOfRepoDocs.Build(new AsOfDocsRequest(repo, initial, _globs, home.Scrubbers), new GitCli(), TimeProvider.System, TestContext.Current.CancellationToken)).Value;
        var atHead = (await AsOfRepoDocs.Build(new AsOfDocsRequest(repo, fixture.Head, _globs, home.Scrubbers), new GitCli(), TimeProvider.System, TestContext.Current.CancellationToken)).Value;

        var before = (await atCut.Search(Query("orientation"), TestContext.Current.CancellationToken)).Value;
        var after = (await atHead.Search(Query("orientation"), TestContext.Current.CancellationToken)).Value;
        Assert.DoesNotContain(before, h => h.Ref.Canonical.Value.Contains("decisions/0001", StringComparison.Ordinal));   // that doc arrives in PR #3
        Assert.Contains(after, h => h.Ref.Canonical.Value == "repo:aw-split-panes/docs/decisions/0001-orientation-toggle.md");
        Assert.True(atCut.Describe().SupportsAsOf);
        Assert.All(after, h => Assert.Equal("repo-docs", h.Ref.Source.Value));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AsOfRepoDocs_scrubs_before_indexing()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), fixture.Path);
        await File.AppendAllTextAsync(Path.Combine(fixture.Path, "docs", "architecture.md"), "\n## Deploy\n\nThe orientation deploy key is " + Planted.Secret + "\n", TestContext.Current.CancellationToken);
        _ = Run(fixture.Path, "commit", "-q", "-am", "plant");
        var head = Run(fixture.Path, "rev-parse", "HEAD");

        var docs = (await AsOfRepoDocs.Build(new AsOfDocsRequest(repo, head, _globs, home.Scrubbers), new GitCli(), TimeProvider.System, TestContext.Current.CancellationToken)).Value;
        var hit = (await docs.Search(Query("deploy"), TestContext.Current.CancellationToken)).Value[0];
        var record = (await docs.Fetch(hit.Ref, TestContext.Current.CancellationToken)).Value;

        Assert.DoesNotContain(Planted.Secret, record.Text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:", record.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AsOfRepoDocs_refs_carry_the_blob_as_version_so_a_later_edit_is_stale()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), fixture.Path);

        var docs = (await AsOfRepoDocs.Build(new AsOfDocsRequest(repo, fixture.Head, _globs, home.Scrubbers), new GitCli(), TimeProvider.System, TestContext.Current.CancellationToken)).Value;
        var hit = (await docs.Search(Query("architecture"), TestContext.Current.CancellationToken)).Value[0];
        var versions = (await docs.Versions([hit.Ref], TestContext.Current.CancellationToken)).Value;

        Assert.Equal(Run(fixture.Path, "rev-parse", "HEAD:docs/architecture.md"), hit.Ref.Version.Value);
        Assert.Equal(hit.Ref.Version, versions[0]);   // the throwaway index is the cut: nothing in it can be stale
    }

    private static string Run(string path, params string[] args) => GitHelper.Run(path, args);
}
