using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// The first-parent history of a repo over <see cref="GitCli"/> (read-only git, the caller's GIT_* variables removed, never the working tree). A merge
/// commit carries the files it changed against its first parent and the merged branch's name when its subject gives one.
/// </summary>
internal sealed partial class GitMergeHistory(GitCli git) : IMergeHistory
{
    public async ValueTask<Result<RepoHistory>> History(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var log = await git.FirstParentLog(repo.Path, cancellationToken).ConfigureAwait(false);
        if (log.IsError)
        {
            return Unreadable(repo);
        }

        var commits = new List<HistoryCommit>(log.Value.Count);
        foreach (var entry in log.Value)
        {
            var isMerge = entry.Parents > 1;
            var files = isMerge ? await git.ChangedAgainstFirstParent(repo.Path, entry.Sha, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<string>>([]);
            if (files.IsError)
            {
                return Unreadable(repo);
            }

            commits.Add(new HistoryCommit(entry.Sha, entry.At, entry.Subject, isMerge ? Branch(entry.Subject) : null, isMerge, files.Value));
        }

        return new RepoHistory(repo.Name, commits);
    }

    private static string? Branch(string subject)
    {
        var match = MergedBranch.Match(subject);
        return match.Success ? match.Groups["branch"].Value : null;
    }

    private static Error Unreadable(ContextRepo repo) =>
        ContextErrors.ExtractionUnreadable.With($"git can't read the history of {repo.Name.Value} (fix: check the checkout)");   // names the repo, never what git printed

    [GeneratedRegex("(?:Merge branch '(?<branch>[^']+)'|from (?<branch>\\S+))", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MergedBranch { get; }
}
