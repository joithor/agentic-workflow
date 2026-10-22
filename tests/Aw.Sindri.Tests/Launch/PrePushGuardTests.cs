using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The refusing pre-push hook, against a real temp repo (LN-LCH1).</summary>
[Lane(TestLane.Integration)]
public sealed class PrePushGuardTests : IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    [Fact]
    public async Task A_missing_hook_is_installed_and_refuses_a_push_where_the_worktree_variable_is_set()
    {
        var repo = await Repo();

        var error = await PrePushGuard.Install(repo.Path, Token);

        Assert.Null(error);
        var hook = HookPath(repo);
        var (Exit, Error) = await RunHook(hook, worktree: "/work/wt");
        Assert.Equal(1, Exit);
        Assert.Contains(SindriErrors.LaunchPushRefused.Code, Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_hook_lets_the_owner_push_where_no_worktree_variable_is_set()
    {
        var repo = await Repo();
        _ = await PrePushGuard.Install(repo.Path, Token);

        Assert.Equal(0, (await RunHook(HookPath(repo), worktree: null)).Exit);
    }

    [Fact]
    public async Task Our_own_hook_is_installed_again_without_error()
    {
        var repo = await Repo();
        _ = await PrePushGuard.Install(repo.Path, Token);

        Assert.Null(await PrePushGuard.Install(repo.Path, Token));
    }

    [Fact]
    public async Task A_foreign_hook_is_never_overwritten_and_refuses_the_launch()
    {
        var repo = await Repo();
        var hook = HookPath(repo);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, "#!/bin/sh\necho mine\n", Token);

        var error = await PrePushGuard.Install(repo.Path, Token);

        Assert.Equal(SindriErrors.LaunchPrePushForeign.Code, error?.Code);
        Assert.Equal("#!/bin/sh\necho mine\n", await File.ReadAllTextAsync(hook, Token));
    }

    [Fact]
    public async Task A_linked_worktree_gets_the_hook_in_the_shared_hooks_directory()
    {
        var repo = await Repo();
        var worktree = Path.Combine(repo.Path, ".claude", "worktrees", "step-1");
        _ = await repo.Git("worktree", "add", "--detach", worktree);

        var error = await PrePushGuard.Install(worktree, Token);

        Assert.Null(error);
        Assert.True(File.Exists(HookPath(repo)));
    }

    [Fact]
    public async Task A_directory_that_is_not_a_repo_is_left_alone()
    {
        using var temp = new TempHome();

        Assert.Null(await PrePushGuard.Install(temp.Path, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string HookPath(TempGitRepo repo) => Path.Combine(repo.Path, ".git", "hooks", "pre-push");

    private static async Task<(int Exit, string Error)> RunHook(string hook, string? worktree)
    {
        var start = new ProcessStartInfo("sh", [hook]) { UseShellExecute = false, RedirectStandardError = true };
        _ = start.Environment.Remove("AW_SINDRI_WORKTREE");
        if (worktree is not null)
        {
            start.Environment["AW_SINDRI_WORKTREE"] = worktree;
        }

        using var process = Process.Start(start)!;   // why: Start returns a process for a command it found
        var error = process.StandardError.ReadToEndAsync(Token);
        await process.WaitForExitAsync(Token);
        return (process.ExitCode, await error);
    }

    private async Task<TempGitRepo> Repo()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return repo;
    }
}
