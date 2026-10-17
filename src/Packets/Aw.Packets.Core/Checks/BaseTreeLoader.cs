using Aw.Kernel;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Checks;

/// <summary>Lists the repository's files at a packet's base commit, for the rules that read it.</summary>
internal static class BaseTreeLoader
{
    internal static async ValueTask<Result<BaseTree>> Load(IRepoSnapshot repo, string sha, string repoDirectory, CancellationToken cancellationToken)
    {
        if (!CommitSha.TryFrom(sha, out var commit))
        {
            return new BaseTree(Files: null, $"baseSha '{sha}' isn't a full commit sha (40 or 64 lower-case hex digits)");
        }

        var files = await repo.Files(repoDirectory, commit, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error.Code == PacketsErrors.CommitNotFound.Code
                ? new BaseTree(Files: null, $"baseSha {sha} isn't in the repository: {files.Error.Message}")
                : files.Error;
        }

        return new BaseTree(files.Value, Problem: null);
    }
}
