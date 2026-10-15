using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Reads a repo's files at a commit through git (never the working tree). Any git failure is SND-CONTEXT-048.</summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitRepoReader(GitCli git) : IRepoReader
{
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) =>
        throw Pending.Because(git);

    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        throw Pending.Because();

    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) =>
        throw Pending.Because();
}
