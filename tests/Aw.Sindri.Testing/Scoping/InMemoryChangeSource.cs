using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IChangeSource"/>: one branch over a base, as a git checkout would answer it.</summary>
public sealed class InMemoryChangeSource : IChangeSource
{
    /// <summary>A source whose <c language="text">feat/expiry</c> branch adds the commits to the base files.</summary>
    /// <param name="baseFiles">The files on the base.</param>
    /// <param name="commits">The branch's commits, oldest first.</param>
    public InMemoryChangeSource(IReadOnlyDictionary<string, string> baseFiles, IReadOnlyList<ChangeSeedCommit> commits)
    {
        ArgumentNullException.ThrowIfNull(baseFiles);
        ArgumentNullException.ThrowIfNull(commits);
    }

    /// <inheritdoc/>
    public ValueTask<Result<ChangeRecord>> Read(string checkout, string reference, ChangeRead read, CancellationToken cancellationToken) => throw new NotSupportedException();
}
