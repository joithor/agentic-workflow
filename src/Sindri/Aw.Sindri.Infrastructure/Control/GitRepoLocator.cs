using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

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

    private static async Task<string?> CommonDir(string directory, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git", ["-C", directory, "rev-parse", "--path-format=absolute", "--git-common-dir"])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            git.Kill();
            return null;
        }
    }
}
