using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitRepoReaderContractTests : RepoReaderContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose() => _repos.ForEach(r => r.Dispose());

    protected override IRepoReader CreateReader(out ContextRepo repo, out string commit)
    {
        var temp = new TempGitRepo(Committed);
        _repos.Add(temp);
        repo = temp.Repo;
        commit = temp.Head;
        return new GitRepoReader(new GitCli());
    }

    protected override IRepoReader CreateChangeReader(out ContextRepo repo, out ChangeWorld world)
    {
        var temp = new TempGitRepo(Committed);
        _repos.Add(temp);
        repo = temp.Repo;
        world = ChangeWorlds.Build(temp);
        return new GitRepoReader(new GitCli());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Changes_ignores_the_repos_diff_renames_setting_and_the_callers_GIT_environment()
    {
        using var temp = new TempGitRepo(Committed);
        var world = ChangeWorlds.Build(temp);
        _ = temp.Run("config", "diff.renames", "copies");
        var reader = new GitRepoReader(new GitCli());
        Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(Path.GetTempPath(), "aw-no-such-gitdir"));
        Result<RepoChanges> changes;
        try
        {
            changes = await reader.Changes(temp.Repo, new DiffRange(world.Head, world.RenameHead), TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_DIR", value: null);
        }

        Assert.True(changes.IsSuccess, changes.IsError ? changes.Error.Message : string.Empty);
        Assert.Equal(world.RenameExpected, changes.Value.Files);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Changes_never_reads_the_working_tree()
    {
        using var temp = new TempGitRepo(Committed);
        var world = ChangeWorlds.Build(temp);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "README.md"), "dirty and uncommitted\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "untracked.ts"), "x\n", TestContext.Current.CancellationToken);
        var reader = new GitRepoReader(new GitCli());

        var changes = await reader.Changes(temp.Repo, new DiffRange(world.MergeBase, world.MergeBase), TestContext.Current.CancellationToken);
        var branch = await reader.Changes(temp.Repo, new DiffRange(world.Base, world.Head), TestContext.Current.CancellationToken);

        Assert.Empty(changes.Value.Files);
        Assert.DoesNotContain(branch.Value.Files, f => string.Equals(f.Path, "untracked.ts", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Changes_reads_a_path_with_spaces_and_non_ascii_characters_as_written()
    {
        using var temp = new TempGitRepo(Committed);
        var odd = "docs/dé tail \u00fcber.md";
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "docs", "dé tail \u00fcber.md"), "x\n", TestContext.Current.CancellationToken);
        _ = temp.Run("add", "-A");
        _ = temp.Run("commit", "-q", "-m", "odd name");
        var reader = new GitRepoReader(new GitCli());

        var changes = await reader.Changes(temp.Repo, new DiffRange(temp.Head, temp.Run("rev-parse", "HEAD")), TestContext.Current.CancellationToken);

        Assert.Equal([odd], changes.Value.Files.Select(f => f.Path), StringComparer.Ordinal);
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
}
