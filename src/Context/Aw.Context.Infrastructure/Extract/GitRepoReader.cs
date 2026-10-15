using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Reads a repo's files at a commit through git (never the working tree), leaving out the paths the repo denies: they are not listed and reading one is refused like a missing path. Any git failure is SND-CONTEXT-048.</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitRepoReader(GitCli git) : IRepoReader
{
    public async ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var head = await git.Head(repo.Path, cancellationToken).ConfigureAwait(false);
        return head.IsError ? Unreadable(repo, "HEAD") : head;
    }

    public async ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var tree = await git.Tree(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return tree.IsError ? Unreadable(repo, commit) : Results.Success<IReadOnlyList<string>>([.. tree.Value.Keys.Where(p => !repo.Denies(p)).Order(StringComparer.Ordinal)]);
    }

    public async ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0 || Path.IsPathRooted(path) || path.Any(char.IsControl) || path.Split('/').Contains("..") || repo.Denies(path))
        {
            return Unreadable(repo, commit, "a path inside the repo");
        }

        var text = await git.Show(repo.Path, commit, path, cancellationToken).ConfigureAwait(false);
        return text.IsError ? Unreadable(repo, commit, path) : text;
    }

    // Names the repo and the commit or path asked for, never what git printed (it may name repo content).
    private static Error Unreadable(ContextRepo repo, string what, string? path = null) =>
        ContextErrors.ExtractionUnreadable.With($"git can't read {path ?? what} of {repo.Name.Value} (fix: check the checkout and that the commit exists)");
}
