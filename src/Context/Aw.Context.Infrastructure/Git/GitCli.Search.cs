using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>The call behind the code lens's lexical search: which files of a commit contain some text.</summary>
internal sealed partial class GitCli
{
    /// <summary>The tracked files of a commit that contain any term ('git grep -l -I -i -F').</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A full commit sha.</param>
    /// <param name="terms">Checked terms: each goes after '-e', so none is read as an option.</param>
    /// <param name="within">When not null, only these literal paths (after '--').</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The matching paths, unordered; an empty list when none match.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> GrepFiles(string repo, string commit, IReadOnlyList<string> terms, IReadOnlyList<string>? within, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        string[] args = ["grep", "-l", "-I", "-i", "-F", "-z", "--no-color", .. terms.SelectMany(t => new[] { "-e", t }), commit, "--", .. (within ?? []).Select(p => ":(literal)" + p)];
        var run = await Run(repo, args, cancellationToken, locale: "C.UTF-8").ConfigureAwait(false);   // UTF-8 so -i folds non-ASCII letters too
        return run switch
        {
            { IsError: true } => run.Error,
            { Value.Exit: 0 } => Results.Success<IReadOnlyList<string>>([.. run.Value.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(p => StripCommit(p, commit))]),
            { Value.Exit: 1 } => Results.Success<IReadOnlyList<string>>([]),   // git grep: 1 is "no match"
            _ => Failed("grep", run.Value.Exit),
        };
    }

    // With a tree-ish git prints "<commit>:<path>".
    private static string StripCommit(string line, string commit) => line.StartsWith(commit + ":", StringComparison.Ordinal) ? line[(commit.Length + 1)..] : line;
}
