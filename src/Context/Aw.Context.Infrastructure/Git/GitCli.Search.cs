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
    public ValueTask<Result<IReadOnlyList<string>>> GrepFiles(string repo, string commit, IReadOnlyList<string> terms, IReadOnlyList<string>? within, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
