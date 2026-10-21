using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// Where each checkout is (spec §6.1): 'git rev-parse HEAD', then 'git merge-base --is-ancestor HEAD &lt;default&gt;' against
/// local refs only, with no fetch; the default branch is origin/HEAD's target, else main, else master. Shared with the
/// context lane, which owns the contract; this block only calls it.
/// </summary>
public interface IRepoHeads
{
    /// <summary>Reads each repo's head and whether it is on the default branch.</summary>
    /// <param name="repos">The repos.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per repo; a repo git cannot read has a null head and is not on the default branch.</returns>
    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(IReadOnlyList<CodeIndexRepo> repos, CancellationToken cancellationToken);

    /// <summary>Whether a commit is on the default branch, local refs only.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check; SND-CODEIDX-002 when the commit is not in the repo.</returns>
    public ValueTask<Result<DefaultBranchCheck>> OnDefaultBranch(CodeIndexRepo repo, CommitSha commit, CancellationToken cancellationToken);
}
