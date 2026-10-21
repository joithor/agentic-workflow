using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>A snapshot implementation holding a <see cref="RepoLayout"/> at one commit; Dispose removes what it made.</summary>
/// <param name="Snapshot">The implementation under test.</param>
/// <param name="Repo">The repo to pass it.</param>
/// <param name="Commit">The commit that holds the layout.</param>
/// <param name="Cleanup">What to dispose when the test ends, or null.</param>
public sealed record ArrangedSnapshot(IRepoSnapshot Snapshot, CodeIndexRepo Repo, CommitSha Commit, IDisposable? Cleanup) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Cleanup?.Dispose();
}
