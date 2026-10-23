using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Tests.Workflows.Effects;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The git adapter of the restack's local work against real repositories: a fetch of the new base, a rebase that drops a squash-merged base's commits, a conflict that leaves the workspace as it was.</summary>
[Lane(TestLane.Integration)]
public sealed class GitWorkspaceRebaseTests : IDisposable
{
    private static readonly string[] _identity = ["-c", "user.email=test@example.invalid", "-c", "user.name=test"];

    private readonly List<TempGitRepo> _repos = [];
    private readonly GitWorkspaceRebase _rebase = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    [Fact]
    public async Task A_squash_merged_base_is_dropped_when_the_rebase_starts_after_its_tip()
    {
        var (origin, workspace, baseTip) = await Stack();
        _ = await origin.Git("checkout", "-q", "main");
        _ = await origin.Git("merge", "--squash", "feat/a");
        _ = await origin.Git([.. _identity, "commit", "-q", "-m", "squash of a"]);

        var main = await _rebase.Fetch(workspace.Path, "main", Token);
        var rebased = await _rebase.Rebase(workspace.Path, main.Value, baseTip, Token);

        Assert.True(rebased.IsSuccess, rebased.IsError ? rebased.Error.Message : null);
        Assert.Equal(await workspace.Git("rev-parse", "HEAD"), rebased.Value.Head);
        Assert.Equal("b1", await workspace.Git("log", "--format=%s", $"{main.Value}..HEAD"));
        Assert.True(File.Exists(Path.Combine(workspace.Path, "a.txt")) && File.Exists(Path.Combine(workspace.Path, "b.txt")));
    }

    [Fact]
    public async Task A_conflict_names_the_paths_and_leaves_the_workspace_on_its_head_with_no_rebase_under_way()
    {
        var (origin, workspace, baseTip) = await Stack();
        var before = await workspace.Git("rev-parse", "HEAD");
        _ = await origin.Git("checkout", "-q", "main");
        await File.WriteAllTextAsync(Path.Combine(origin.Path, "b.txt"), "main's b\n", Token);
        _ = await origin.Git("add", "b.txt");
        _ = await origin.Git([.. _identity, "commit", "-q", "-m", "main touches b"]);

        var main = await _rebase.Fetch(workspace.Path, "main", Token);
        var rebased = await _rebase.Rebase(workspace.Path, main.Value, baseTip, Token);

        Assert.True(rebased.IsSuccess, rebased.IsError ? rebased.Error.Message : null);
        Assert.Null(rebased.Value.Head);
        Assert.Equal(["b.txt"], rebased.Value.Conflicts);
        Assert.Equal(before, await workspace.Git("rev-parse", "HEAD"));
        Assert.False(Directory.Exists(Path.Combine(workspace.Path, ".git", "rebase-merge")));
        Assert.Equal(string.Empty, await workspace.Git("status", "--porcelain"));
    }

    [Fact]
    public async Task A_base_that_moved_on_is_rebased_onto_without_a_from()
    {
        var (origin, workspace, _) = await Stack();
        _ = await origin.Git("checkout", "-q", "feat/a");
        await File.WriteAllTextAsync(Path.Combine(origin.Path, "c.txt"), "c\n", Token);
        _ = await origin.Git("add", "c.txt");
        _ = await origin.Git([.. _identity, "commit", "-q", "-m", "a2"]);

        var tip = await _rebase.Fetch(workspace.Path, "feat/a", Token);
        var rebased = await _rebase.Rebase(workspace.Path, tip.Value, from: null, Token);

        Assert.Equal(await workspace.Git("rev-parse", "HEAD"), rebased.Value.Head);
        Assert.Equal("b1", await workspace.Git("log", "--format=%s", $"{tip.Value}..HEAD"));
    }

    [Fact]
    public async Task Contains_tells_an_ancestor_from_a_commit_that_is_not()
    {
        var (_, workspace, baseTip) = await Stack();
        var head = await workspace.Git("rev-parse", "HEAD");

        Assert.True((await _rebase.Contains(workspace.Path, baseTip, head, Token)).Value);
        Assert.False((await _rebase.Contains(workspace.Path, head, baseTip, Token)).Value);
    }

    [Fact]
    public async Task Reset_puts_the_branch_and_files_back()
    {
        var (_, workspace, baseTip) = await Stack();

        var reset = await _rebase.Reset(workspace.Path, baseTip, Token);

        Assert.True(reset.IsSuccess);
        Assert.Equal(baseTip, await workspace.Git("rev-parse", "HEAD"));
        Assert.False(File.Exists(Path.Combine(workspace.Path, "b.txt")));
    }

    [Fact]
    public async Task A_branch_name_that_could_be_an_option_is_refused_before_git_runs()
    {
        var (_, workspace, _) = await Stack();

        var fetched = await _rebase.Fetch(workspace.Path, "--upload-pack=x", Token);

        Assert.True(fetched.IsError);
    }

    // main: c0; feat/a: a1 (a.txt); feat/b: b1 (b.txt) on a1; the workspace is a clone checked out on feat/b.
    private async Task<(TempGitRepo Origin, TempGitRepo Workspace, string BaseTip)> Stack()
    {
        var origin = await TempGitRepo.Create();
        _repos.Add(origin);
        _ = await origin.Git("checkout", "-q", "-b", "feat/a");
        await File.WriteAllTextAsync(Path.Combine(origin.Path, "a.txt"), "a\n", Token);
        _ = await origin.Git("add", "a.txt");
        _ = await origin.Git([.. _identity, "commit", "-q", "-m", "a1"]);
        var baseTip = await origin.Git("rev-parse", "HEAD");
        _ = await origin.Git("checkout", "-q", "-b", "feat/b");
        await File.WriteAllTextAsync(Path.Combine(origin.Path, "b.txt"), "b\n", Token);
        _ = await origin.Git("add", "b.txt");
        _ = await origin.Git([.. _identity, "commit", "-q", "-m", "b1"]);
        var workspace = await TempGitRepo.Create(commits: 0);
        _repos.Add(workspace);
        _ = await workspace.Git("remote", "add", "origin", origin.Path);
        _ = await workspace.Git("fetch", "-q", "origin");
        _ = await workspace.Git("checkout", "-q", "-b", "feat/b", "origin/feat/b");
        return (origin, workspace, baseTip);
    }
}
