using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Repo resolution through git (API spec §3.1): the main checkout is the parent of 'git rev-parse --path-format=absolute
/// --git-common-dir' (so a linked worktree, or a submodule, resolves to its repository), resolved to its real path, and
/// matched against the entries' real paths. Outside git, without git, or when git doesn't answer within 5 s, there is no repo.
/// </summary>
internal sealed class GitRepoLocator : IRepoLocator
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(5);

    public async ValueTask<RepoName?> Locate(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
    {
        if (repos.Count == 0 || await CommonDir(directory, cancellationToken).ConfigureAwait(false) is not { } commonDir)
        {
            return null;
        }

        var checkout = Path.GetDirectoryName(commonDir.TrimEnd('/')) ?? "/";
        return RepoScope.Longest(RealPath.Of(checkout) ?? checkout, [.. repos.Select(r => r with { Path = RealPath.Of(r.Path) ?? r.Path })]);   // a link loop: kept as written
    }

    public async ValueTask<RepoPlacement?> Place(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
    {
        if (await Git(directory, ["--show-toplevel", "--git-dir", "--git-common-dir"], cancellationToken).ConfigureAwait(false) is not { } answer
            || answer.Split('\n') is not [var top, var gitDir, var common])
        {
            return null;
        }

        var topReal = RealPath.Of(top) ?? top;
        var main = MainCheckout(topReal, gitDir, common);
        var name = repos
            .Where(r => (RealPath.Of(r.Path) ?? r.Path) is var real && (Same(real, topReal) || (main is not null && Same(real, main))) && RepoName.TryFrom(r.Name, out _))
            .Select(static r => (RepoName?)RepoName.From(r.Name))
            .FirstOrDefault();
        return new RepoPlacement(topReal, name);
    }

    private static bool Same(string a, string b) => string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.Ordinal);

    /// <summary>
    /// The main checkout of the repository at <paramref name="top"/>. A main checkout's git dir is its common dir (a separate git dir or a submodule included). A linked worktree's main checkout
    /// is the common dir's parent only when <c lang="text">.git</c> there is the common dir itself; a worktree of a bare repo, of a separate-git-dir checkout or of a submodule has none, so it is null.
    /// </summary>
    private static string? MainCheckout(string top, string gitDir, string common)
    {
        var commonReal = RealPath.Of(common) ?? common;
        if (string.Equals(RealPath.Of(gitDir) ?? gitDir, commonReal, StringComparison.Ordinal))
        {
            return top;
        }

        var candidate = Path.GetDirectoryName(common.TrimEnd('/'));
        return candidate is not null && string.Equals(RealPath.Of(Path.Combine(candidate, ".git")) ?? string.Empty, commonReal, StringComparison.Ordinal)
            ? RealPath.Of(candidate) ?? candidate
            : null;
    }

    private static Task<string?> CommonDir(string directory, CancellationToken cancellationToken) => Git(directory, ["--git-common-dir"], cancellationToken);

    private static async Task<string?> Git(string directory, string[] queries, CancellationToken cancellationToken)
    {
        var start = ChildEnvironment.Start("git", ["-C", directory, "rev-parse", "--path-format=absolute", .. queries]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        Process? git;
        try
        {
            git = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // no git on PATH
        }

        using (git)
        {
            return git is null ? null : await Answer(git, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> Answer(Process git, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = git.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = git.StandardError.ReadToEndAsync(deadline.Token);
            await git.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var text = (await output.ConfigureAwait(false)).Trim();
            _ = await error.ConfigureAwait(false);   // 'not a git repository' and the like: no repo
            return git.ExitCode == 0 && text.Length > 0 ? text : null;
        }
        catch (OperationCanceledException)
        {
            git.Kill();   // the budget or the caller's own cancellation: never leave git running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
    }
}
