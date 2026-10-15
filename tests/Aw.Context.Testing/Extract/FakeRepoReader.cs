using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An in-memory repo at one commit that leaves out the paths its repo denies; <see cref="RepoReaderContract"/> holds it to the git reader's behavior.</summary>
/// <param name="known">The repo it answers for.</param>
/// <param name="head">Its only commit.</param>
/// <param name="files">The files at that commit.</param>
public sealed class FakeRepoReader(ContextRepo known, string head, IReadOnlyDictionary<string, string> files) : IRepoReader
{
    /// <inheritdoc/>
    public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(Knows(repo) ? head : Unreadable);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Knows(repo) && commit == head ? Results.Success<IReadOnlyList<string>>([.. files.Keys.Where(f => !repo.Denies(f)).Order(StringComparer.Ordinal)]) : Unreadable);

    /// <inheritdoc/>
    public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ValueTask.FromResult<Result<string>>(Knows(repo) && commit == head && !repo.Denies(path) && files.TryGetValue(path, out var text) ? text : Unreadable);
    }

    private static Error Unreadable => ContextErrors.ExtractionUnreadable.With("the fake has no such repo, commit or path");

    private bool Knows(ContextRepo repo) => repo.Name == known.Name && string.Equals(repo.Path, known.Path, StringComparison.Ordinal);
}
