using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The git adapter against a real temp repository with a linked worktree (git worktree add) beside the main checkout, not
/// inside it, so no path-prefix or show-toplevel shortcut can pass. The temp directory is named through its unresolved
/// path (on macOS /var is a link to /private/var), so the match runs on real paths.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GitRepoLocatorContractTests : RepoLocatorContract, IDisposable
{
    private readonly TempHome _temp = new();

    public void Dispose() => _temp.Dispose();

    protected override IRepoLocator Create() => new GitRepoLocator();

    protected override async ValueTask<(string MainCheckout, string InWorktree)> RepoWithWorktree()
    {
        var main = Path.Combine(_temp.Path, "main");
        var worktree = Path.Combine(_temp.Path, "wt-x");   // outside the main checkout: only the common dir can name 'main'
        _ = Directory.CreateDirectory(main);
        await Git(main, IsolatedGit.Init());
        await Git(main, [.. IsolatedGit.NoHooks, "-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "--allow-empty", "-m", "init"]);
        await Git(main, "worktree", "add", "-q", worktree);
        var inside = Path.Combine(worktree, "src");
        _ = Directory.CreateDirectory(inside);
        return (main, inside);
    }

    protected override ValueTask<string> OutsideGit()
    {
        var outside = Path.Combine(_temp.Path, "outside");
        _ = Directory.CreateDirectory(outside);
        return ValueTask.FromResult(outside);
    }

    private static async Task Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git", ["-C", directory, .. args]) { UseShellExecute = false, RedirectStandardError = true };
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var git = Process.Start(start)!;   // why: Start returns a process for a command it found
        var error = await git.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await git.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(git.ExitCode == 0, $"git {string.Join(' ', args)}: {error}");
    }
}
