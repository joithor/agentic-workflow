using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The code index's <see cref="IRepoHeads"/> over the context engine's git readers (spec §6.1): 'git rev-parse HEAD', then whether the default branch holds it, local refs only.
/// A repo git cannot read has no head and is not on the default branch, as the port says.
/// </summary>
/// <param name="repos">Reads a checkout's head.</param>
/// <param name="defaults">Whether the default branch holds a commit.</param>
internal sealed class ContextRepoHeads(IRepoReader repos, IDefaultBranch defaults) : IRepoHeads
{
    private const string DefaultBranchWord = "default";

    public async ValueTask<Result<IReadOnlyList<IndexRepoHead>>> Heads(IReadOnlyList<CodeIndexRepo> list, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        var heads = new List<IndexRepoHead>(list.Count);
        foreach (var repo in list)
        {
            heads.Add(await One(repo, cancellationToken).ConfigureAwait(false));
        }

        return heads;
    }

    public async ValueTask<Result<DefaultBranchCheck>> OnDefaultBranch(CodeIndexRepo repo, CommitSha commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var reaches = await defaults.Reaches(Context(repo), commit.Value, cancellationToken).ConfigureAwait(false);
        return reaches.IsError ? CodeIndexErrors.RepoNotFound.With("the commit can't be checked against the default branch") : new DefaultBranchCheck(DefaultBranchWord, reaches.Value);
    }

    private async ValueTask<IndexRepoHead> One(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var context = Context(repo);
        var head = await repos.Head(context, cancellationToken).ConfigureAwait(false);
        if (head.IsError || !CommitSha.TryFrom(head.Value, out var sha))
        {
            return new IndexRepoHead(repo.Name, Head: null, OnDefaultBranch: false);
        }

        var onDefault = await defaults.Reaches(context, head.Value, cancellationToken).ConfigureAwait(false);
        return new IndexRepoHead(repo.Name, sha, !onDefault.IsError && onDefault.Value);
    }

    private static ContextRepo Context(CodeIndexRepo repo) => new(ContextRepoName.From(repo.Name.Value), repo.Path);
}
