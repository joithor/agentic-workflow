using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>Repo resolution through git.</summary>
internal sealed class GitRepoLocator : IRepoLocator
{
    public ValueTask<RepoName?> Locate(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: git common dir, real paths, longest entry");
}
