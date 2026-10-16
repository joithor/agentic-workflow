using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>The calls behind the diff shape: resolve a sha to a commit, find the merge base, list the changed paths.</summary>
internal sealed partial class GitCli
{
    /// <summary>The full sha of the commit a sha names, so an abbreviated one is expanded and a missing one is told apart from an unreadable repo.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="sha">An abbreviated or full commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The full sha; SND-CONTEXT-110 when the repo holds no such commit; SND-CONTEXT-010 when git can't answer (a missing checkout, an ambiguous abbreviation).</returns>
    public async ValueTask<Result<string>> Commit(string repo, string sha, CancellationToken cancellationToken)
    {
        if (!IsSha(sha))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var run = await Run(repo, ["rev-parse", "--verify", "--quiet", sha + "^{commit}"], cancellationToken).ConfigureAwait(false);
        return run switch
        {
            { IsError: true } => run.Error,
            { Value.Exit: 0 } when IsSha(run.Value.Stdout.Trim()) => run.Value.Stdout.Trim(),
            { Value.Exit: 1 } => ContextErrors.CommitUnknown.With("the repo holds no such commit"),
            _ => Failed("rev-parse", run.Value.Exit),
        };
    }

    /// <summary>The best common ancestor of two commits.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="first">A full commit sha.</param>
    /// <param name="second">A full commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The merge base's sha; SND-CONTEXT-111 when the commits share no history; SND-CONTEXT-010 when git can't answer.</returns>
    public async ValueTask<Result<string>> MergeBase(string repo, string first, string second, CancellationToken cancellationToken)
    {
        if (!IsSha(first) || !IsSha(second))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var run = await Run(repo, ["merge-base", first, second], cancellationToken).ConfigureAwait(false);
        return run switch
        {
            { IsError: true } => run.Error,
            { Value.Exit: 0 } when IsSha(run.Value.Stdout.Trim()) => run.Value.Stdout.Trim(),
            { Value.Exit: 1 } => ContextErrors.NoMergeBase.With("the commits share no history"),
            _ => Failed("merge-base", run.Value.Exit),
        };
    }

    /// <summary>The paths that differ between two commits and how (a rename is a deletion plus an addition). Reads the object database only, never the working tree or file content.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="from">A full commit sha (the old side).</param>
    /// <param name="to">A full commit sha (the new side).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The changes in git's order; SND-CONTEXT-010 when git can't answer.</returns>
    public async ValueTask<Result<IReadOnlyList<RepoChange>>> NameStatus(string repo, string from, string to, CancellationToken cancellationToken)
    {
        if (!IsSha(from) || !IsSha(to))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var listed = await Text(repo, ["diff", "--name-status", "-z", "--no-renames", "--no-ext-diff", "--no-textconv", from, to, "--"], cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyList<RepoChange>>(text =>
        {
            var tokens = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            return [.. Enumerable.Range(0, tokens.Length / 2).Select(i => new RepoChange(tokens[(2 * i) + 1], KindOf(tokens[2 * i])))];
        });
    }

    // Anything other than an addition, deletion or type change reads as modified (a letter git adds later is never a throw).
    private static ChangeKind KindOf(string letter) => letter switch
    {
        "A" => ChangeKind.Added,
        "D" => ChangeKind.Deleted,
        "T" => ChangeKind.TypeChanged,
        _ => ChangeKind.Modified,
    };
}
