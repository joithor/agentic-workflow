using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IRepoCheckouts"/>: the checkouts it was given by repo name.</summary>
public sealed class InMemoryRepoCheckouts : IRepoCheckouts
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);

    /// <summary>Holds a checkout.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="path">Its checkout path.</param>
    /// <returns>The same instance.</returns>
    public InMemoryRepoCheckouts With(string repo, string path)
    {
        _paths[repo] = path;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Of(RepoName repo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_paths.TryGetValue(repo.Value, out var path) ? Results.Success(path) : SindriErrors.InvalidInput.With($"the repo {repo.Value} has no checkout"));
    }
}
