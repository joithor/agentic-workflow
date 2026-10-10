using Aw.Kernel;
using Aw.Packets.Core;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="IRepoSnapshot"/>: commits as file sets; a repository can be marked unreadable.</summary>
internal sealed class InMemoryRepoSnapshot : IRepoSnapshot
{
    private readonly Dictionary<string, IReadOnlySet<string>> _commits = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    /// <summary>Adds a commit.</summary>
    /// <param name="sha">Its sha.</param>
    /// <param name="files">Its files.</param>
    /// <returns>This fake.</returns>
    public InMemoryRepoSnapshot With(string sha, IReadOnlySet<string> files)
    {
        _commits[sha] = files;
        return this;
    }

    /// <summary>Makes every read of <paramref name="repoDirectory"/> fail with AW-PACKETS-006.</summary>
    /// <param name="repoDirectory">The repository directory.</param>
    /// <returns>This fake.</returns>
    public InMemoryRepoSnapshot Unreadable(string repoDirectory)
    {
        _ = _unreadable.Add(repoDirectory);
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlySet<string>>> Files(string repoDirectory, string commitSha, CancellationToken cancellationToken)
    {
        var result = _unreadable.Contains(repoDirectory)
            ? PacketsErrors.RepoUnavailable.With($"{repoDirectory} is not readable.")
            : _commits.TryGetValue(commitSha, out var files)
                ? Results.Success(files)
                : PacketsErrors.CommitNotFound.With($"Commit {commitSha} is not in the repository.");
        return ValueTask.FromResult(result);
    }
}
