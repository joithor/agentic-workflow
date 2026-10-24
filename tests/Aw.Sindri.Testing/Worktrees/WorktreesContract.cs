using Aw.Sindri.Core.Worktrees;
using Xunit;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>
/// The contract every <see cref="IWorktrees"/> honours (engine spec §6.5): create is idempotent for the same request and an
/// error for the same path or branch at a different base; git failures are told from conflicts and dirty trees; remove
/// never discards uncommitted work and removing what is gone succeeds.
/// </summary>
public abstract class WorktreesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter, a repo with two commits and a place for worktrees.</summary>
    /// <returns>The fixture.</returns>
    protected abstract ValueTask<WorktreeFixture> Create();

    private static WorktreeRequest Request(WorktreeFixture f, string name = "run-1", string? baseCommit = null, string? branch = null) =>
        new(f.RepoRoot, baseCommit ?? f.Base, branch ?? $"sindri/{name}", Path.Combine(f.Parent, name));

    /// <summary>A fresh request creates the checkout at the base, on the branch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_fresh_worktree_is_created_at_the_base()
    {
        var f = await Create();
        var request = Request(f);

        var made = await f.Worktrees.Create(request, Token);

        Expect.True(made.IsSuccess);
        Expect.Equal(request.Path, made.Value.Path);
        Expect.Equal(f.Base, made.Value.Head);
        Expect.Equal(request.Branch, made.Value.Branch);
    }

    /// <summary>A branch a worktree made resolves to the commit it sits at.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_resolves_to_its_commit()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);

        var tip = await f.Worktrees.Resolve(f.RepoRoot, made.Value.Branch, Token);

        Expect.True(tip.IsSuccess);
        Expect.Equal(f.Base, tip.Value);
    }

    /// <summary>A branch the repo does not have is a git failure, never a guess.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_branch_does_not_resolve()
    {
        var f = await Create();

        var tip = await f.Worktrees.Resolve(f.RepoRoot, "sindri/nowhere", Token);

        Expect.True(tip.IsError);
        Expect.Equal(Aw.Sindri.Core.SindriErrors.WorktreeGitFailed.Code, tip.Error.Code);
    }

    /// <summary>Asking twice for the same checkout returns it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_same_request_twice_returns_the_same_checkout()
    {
        var f = await Create();
        var first = await f.Worktrees.Create(Request(f), Token);

        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(first.IsSuccess && again.IsSuccess);
        Expect.Equal(first.Value, again.Value);
    }

    /// <summary>The path exists at another base: an error, and the checkout is not moved.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_different_base_at_an_existing_path_conflicts()
    {
        var f = await Create();
        _ = await f.Worktrees.Create(Request(f), Token);

        var other = await f.Worktrees.Create(Request(f, baseCommit: f.OtherBase), Token);
        var kept = await f.Worktrees.Create(Request(f), Token);

        Expect.True(other.IsError);
        Expect.Equal("SND-WT-002", other.Error.Code);
        Expect.True(kept.IsSuccess && kept.Value.Head == f.Base);
    }

    /// <summary>The path exists on another branch: an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_different_branch_at_an_existing_path_conflicts()
    {
        var f = await Create();
        _ = await f.Worktrees.Create(Request(f), Token);

        var other = await f.Worktrees.Create(Request(f, branch: "sindri/other"), Token);

        Expect.True(other.IsError);
        Expect.Equal("SND-WT-002", other.Error.Code);
    }

    /// <summary>A branch that is checked out in another worktree is in use.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_checked_out_elsewhere_conflicts()
    {
        var f = await Create();
        _ = await f.Worktrees.Create(Request(f, "run-1", branch: "sindri/shared"), Token);

        var other = await f.Worktrees.Create(Request(f, "run-2", branch: "sindri/shared"), Token);

        Expect.True(other.IsError);
        Expect.Equal("SND-WT-002", other.Error.Code);
    }

    /// <summary>A branch left behind at another commit is not reset.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_left_over_branch_at_another_base_conflicts()
    {
        var f = await Create();
        var first = await f.Worktrees.Create(Request(f), Token);
        _ = await f.Worktrees.Remove(first.Value.Path, Token);

        var other = await f.Worktrees.Create(Request(f, "run-2", baseCommit: f.OtherBase, branch: first.Value.Branch), Token);

        Expect.True(other.IsError);
        Expect.Equal("SND-WT-002", other.Error.Code);
    }

    /// <summary>A left-over branch at the same base is reused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_left_over_branch_at_the_same_base_is_reused()
    {
        var f = await Create();
        var first = await f.Worktrees.Create(Request(f), Token);
        _ = await f.Worktrees.Remove(first.Value.Path, Token);

        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(again.IsSuccess);
        Expect.Equal(f.Base, again.Value.Head);
    }

    /// <summary>A sha the repo doesn't have is a git failure.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_sha_is_a_git_failure()
    {
        var f = await Create();

        var made = await f.Worktrees.Create(Request(f, baseCommit: new string('f', 40)), Token);

        Expect.True(made.IsError);
        Expect.Equal("SND-WT-003", made.Error.Code);
    }

    /// <summary>A repo that isn't there is a git failure.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_repo_is_a_git_failure()
    {
        var f = await Create();

        var made = await f.Worktrees.Create(Request(f) with { RepoRoot = Path.Combine(f.Parent, "no-such-repo") }, Token);

        Expect.True(made.IsError);
        Expect.Equal("SND-WT-003", made.Error.Code);
    }

    /// <summary>A clean worktree goes, and its path can be used again.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_clean_worktree_is_removed()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);

        var removed = await f.Worktrees.Remove(made.Value.Path, Token);
        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(removed.IsSuccess);
        Expect.True(again.IsSuccess);
    }

    /// <summary>A dirty worktree is refused and kept.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_dirty_worktree_is_refused_and_kept()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);
        await f.MakeDirty(made.Value.Path);

        var removed = await f.Worktrees.Remove(made.Value.Path, Token);
        var kept = await f.Worktrees.Create(Request(f), Token);

        Expect.True(removed.IsError);
        Expect.Equal("SND-WT-004", removed.Error.Code);
        Expect.True(kept.IsSuccess);
    }

    /// <summary>Removing what is already gone succeeds (a retried cleanup).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Removing_twice_succeeds()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);
        _ = await f.Worktrees.Remove(made.Value.Path, Token);

        var again = await f.Worktrees.Remove(made.Value.Path, Token);

        Expect.True(again.IsSuccess);
    }

    /// <summary>A relative path to remove is invalid.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_relative_remove_path_is_invalid()
    {
        var f = await Create();

        var removed = await f.Worktrees.Remove("relative/run-1", Token);

        Expect.True(removed.IsError);
        Expect.Equal("SND-WT-001", removed.Error.Code);
    }

    /// <summary>A short sha, an upper-case sha and a ref are not a base.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_base_that_is_not_a_full_lower_case_sha_is_invalid()
    {
        var f = await Create();

        foreach (var bad in new[] { f.Base[..7], f.Base.ToUpperInvariant(), "main", string.Empty })
        {
            var made = await f.Worktrees.Create(Request(f, baseCommit: bad), Token);

            Expect.True(made.IsError);
            Expect.Equal("SND-WT-001", made.Error.Code);
        }
    }

    /// <summary>A relative path or repo root is invalid.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_relative_path_or_repo_root_is_invalid()
    {
        var f = await Create();

        var path = await f.Worktrees.Create(Request(f) with { Path = "run-1" }, Token);
        var root = await f.Worktrees.Create(Request(f) with { RepoRoot = "repo" }, Token);

        Expect.True(path.IsError && root.IsError);
        Expect.Equal("SND-WT-001", path.Error.Code);
        Expect.Equal("SND-WT-001", root.Error.Code);
    }

    /// <summary>An empty branch or one that looks like an option is invalid.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unusable_branch_is_invalid()
    {
        var f = await Create();

        foreach (var bad in new[] { string.Empty, "-b", "has space" })
        {
            var made = await f.Worktrees.Create(Request(f, branch: bad), Token);

            Expect.True(made.IsError);
            Expect.Equal("SND-WT-001", made.Error.Code);
        }
    }

    /// <summary>A path inside another worktree is invalid.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_path_inside_another_worktree_is_invalid()
    {
        var f = await Create();
        var outer = await f.Worktrees.Create(Request(f), Token);

        var nested = await f.Worktrees.Create(Request(f, "run-2") with { Path = Path.Combine(outer.Value.Path, "inner") }, Token);

        Expect.True(nested.IsError);
        Expect.Equal("SND-WT-001", nested.Error.Code);
    }

    /// <summary>The engine's real place, inside the main checkout, is allowed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_path_inside_the_main_checkout_is_allowed()
    {
        var f = await Create();
        var request = Request(f) with { Path = Path.Combine(f.RepoRoot, ".claude", "worktrees", "run-1") };

        var made = await f.Worktrees.Create(request, Token);
        var again = await f.Worktrees.Create(request, Token);

        Expect.True(made.IsSuccess && again.IsSuccess);
        Expect.Equal(request.Path, made.Value.Path);
    }

    /// <summary>Commits made in the checkout since: a repeated Create parks instead of answering a checkout that is no longer at the base.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_head_moved_after_create_conflicts()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);
        await f.Advance(made.Value.Path);

        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(again.IsError);
        Expect.Equal("SND-WT-002", again.Error.Code);
    }

    /// <summary>A detached HEAD at the path is not the branch asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_detached_head_conflicts()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);
        await f.Detach(made.Value.Path);

        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(again.IsError);
        Expect.Equal("SND-WT-002", again.Error.Code);
    }

    /// <summary>A worktree whose directory is gone is not answered as a checkout: the stale entry is dropped and a new one made.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_worktree_whose_directory_is_gone_is_made_again()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f), Token);
        await f.DeleteDirectory(made.Value.Path);

        var again = await f.Worktrees.Create(Request(f), Token);

        Expect.True(again.IsSuccess);
        Expect.Equal(f.Base, again.Value.Head);
    }

    /// <summary>Removing a worktree whose directory is gone succeeds and frees its branch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Removing_a_worktree_whose_directory_is_gone_frees_its_branch()
    {
        var f = await Create();
        var made = await f.Worktrees.Create(Request(f, branch: "sindri/shared"), Token);
        await f.DeleteDirectory(made.Value.Path);

        var removed = await f.Worktrees.Remove(made.Value.Path, Token);
        var other = await f.Worktrees.Create(Request(f, "run-2", branch: "sindri/shared"), Token);

        Expect.True(removed.IsSuccess);
        Expect.True(other.IsSuccess);
    }
}
