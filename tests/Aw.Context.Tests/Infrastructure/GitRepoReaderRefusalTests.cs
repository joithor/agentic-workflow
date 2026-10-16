using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>What the git reader refuses before it runs git: no process starts, so this is a unit test (the contract tests beside it run git).</summary>
[Trait("Lane", "unit")]
public sealed class GitRepoReaderRefusalTests
{
    [Fact]
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
