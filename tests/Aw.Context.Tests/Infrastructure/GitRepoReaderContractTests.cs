using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitRepoReaderContractTests : RepoReaderContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose() => _repos.ForEach(r => r.Dispose());

    protected override IRepoReader CreateReader(out ContextRepo repo, out string commit)
    {
        var temp = new TempGitRepo(Files);
        _repos.Add(temp);
        repo = temp.Repo;
        commit = temp.Head;
        return new GitRepoReader(new GitCli());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_build_reads_the_commit_never_the_working_tree()
    {
        using var temp = new TempGitRepo(Files);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "src", "commands", "registry.ts"), "edited and uncommitted\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "untracked.ts"), "x\n", TestContext.Current.CancellationToken);
        var reader = new GitRepoReader(new GitCli());

        var text = await reader.Read(temp.Repo, temp.Head, "src/commands/registry.ts", TestContext.Current.CancellationToken);
        var listed = await reader.ListFiles(temp.Repo, temp.Head, TestContext.Current.CancellationToken);

        Assert.Equal(Files["src/commands/registry.ts"], text.Value);
        Assert.DoesNotContain("untracked.ts", listed.Value, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_commit_that_is_not_a_sha_is_refused_without_running_git()
    {
        var reader = new GitRepoReader(new GitCli());
        var repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), Path.GetTempPath());

        var listed = await reader.ListFiles(repo, "--output=/tmp/x", TestContext.Current.CancellationToken);
        var read = await reader.Read(repo, "HEAD", "a.ts", TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-048", listed.Error.Code);
        Assert.Equal("SND-CONTEXT-048", read.Error.Code);
    }
}
