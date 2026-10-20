using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IRepoLocator"/> honours (API spec §3.1), run against the fake and the git adapter.</summary>
public abstract class RepoLocatorContract
{
    /// <summary>The locator under test.</summary>
    /// <returns>The locator.</returns>
    protected abstract IRepoLocator Create();

    /// <summary>A repository: its main checkout, as a profile would name it, and a directory inside one of its linked worktrees.</summary>
    /// <returns>The two paths.</returns>
    protected abstract ValueTask<(string MainCheckout, string InWorktree)> RepoWithWorktree();

    /// <summary>A directory that isn't in any git repository.</summary>
    /// <returns>The directory.</returns>
    protected abstract ValueTask<string> OutsideGit();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A directory in a worktree resolves to the repo entry of its main checkout.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_worktree_resolves_to_its_main_checkout()
    {
        var (main, inWorktree) = await RepoWithWorktree();

        var repo = await Create().Locate(inWorktree, [new RepoEntry("demo", main)], Token);

        Expect.Equal(RepoName.From("demo"), repo);
    }

    /// <summary>When several entries contain the checkout, the longest path wins.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_longest_containing_entry_wins()
    {
        var (main, inWorktree) = await RepoWithWorktree();
        var parent = main[..main.TrimEnd('/').LastIndexOf('/')];

        var repo = await Create().Locate(inWorktree, [new RepoEntry("outer", parent), new RepoEntry("inner", main), new RepoEntry("other", main + "-other")], Token);

        Expect.Equal(RepoName.From("inner"), repo);
    }

    /// <summary>A checkout no entry contains, and a directory outside git, have no repo.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Outside_every_entry_or_outside_git_there_is_no_repo()
    {
        var (main, inWorktree) = await RepoWithWorktree();
        var locator = Create();

        var elsewhere = await locator.Locate(inWorktree, [new RepoEntry("other", main + "-other")], Token);
        var outside = await locator.Locate(await OutsideGit(), [new RepoEntry("demo", main)], Token);

        Expect.Equal(expected: null, elsewhere);
        Expect.Equal(expected: null, outside);
    }

    /// <summary>A worktree is placed at its own top level and matched to the entry of its main checkout.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_worktree_is_placed_on_the_entry_of_its_main_checkout()
    {
        var (main, inWorktree) = await RepoWithWorktree();

        var placed = await Create().Place(inWorktree, [new RepoEntry("demo", main)], Token);

        Expect.Equal(RepoName.From("demo"), placed?.Repo);
        Expect.Equal(expected: true, placed is not null);
    }

    /// <summary>A repository no entry names is placed with no repo; a directory outside git is not placed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_repo_no_entry_names_is_placed_without_a_repo_and_outside_git_is_not_placed()
    {
        var (main, inWorktree) = await RepoWithWorktree();
        var locator = Create();

        var elsewhere = await locator.Place(inWorktree, [new RepoEntry("other", main + "-other")], Token);
        var outside = await locator.Place(await OutsideGit(), [new RepoEntry("demo", main)], Token);

        Expect.Equal(expected: true, elsewhere is not null);
        Expect.Equal(expected: null, elsewhere?.Repo);
        Expect.Equal(expected: null, outside);
    }
}
