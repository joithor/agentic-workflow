using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>The calls behind the history lens: which commits touched a set of paths, and whether the clone is shallow.</summary>
internal sealed partial class GitCli
{
    /// <summary>Starts every commit record in <see cref="FileLog"/>'s output.</summary>
    internal const char LogRecordStart = '\u001e';

    /// <summary>Separates the fields of a commit record in <see cref="FileLog"/>'s output.</summary>
    internal const char LogFieldSeparator = '\u001f';

    /// <summary>Whether the clone is shallow (its older history is missing).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True for a shallow clone; SND-CONTEXT-010 when git can't answer.</returns>
    public async ValueTask<Result<bool>> IsShallow(string repo, CancellationToken cancellationToken)
    {
        var answer = await Text(repo, ["rev-parse", "--is-shallow-repository"], cancellationToken).ConfigureAwait(false);
        return answer.Bind<bool>(text => text.Trim() switch
        {
            "true" => true,
            "false" => false,
            _ => ContextErrors.SourceUnavailable.With("git rev-parse --is-shallow-repository didn't answer true or false"),
        });
    }

    /// <summary>
    /// The first-parent commits from <paramref name="rev"/> back that changed any of <paramref name="paths"/>, with the files each changed (a merge against its first parent), as one
    /// 'git log'. Each record is the record-start character, sha, committer date (ISO 8601), parents and subject separated by the field separator, a NUL, a newline, and the
    /// changed paths each ended by a NUL. Only the subject is asked for: no body is ever read. Paths go after '--' as literal pathspecs (no glob, no magic).
    /// </summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="rev">A full commit sha.</param>
    /// <param name="paths">The caller has checked them (no control character, none empty).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The raw log text, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<string>> FileLog(string repo, string rev, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        if (!IsSha(rev))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var format = $"--format={LogRecordStart}%H{LogFieldSeparator}%cI{LogFieldSeparator}%P{LogFieldSeparator}%s";
        string[] args = ["log", "--first-parent", "--diff-merges=first-parent", "--no-renames", "--no-color", "--name-only", "-z", format, rev, "--", .. paths.Select(p => ":(literal)" + p)];
        return await Text(repo, args, cancellationToken).ConfigureAwait(false);
    }
}
