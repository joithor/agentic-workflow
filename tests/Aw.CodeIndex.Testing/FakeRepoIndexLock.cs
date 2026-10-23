using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A per-repo index lock that never waits: a taker of a held repo gets SND-CODEIDX-016 at once, whatever its wait. Tests read <see cref="IsHeld"/> to see a lock is released.</summary>
public sealed class FakeRepoIndexLock : IRepoIndexLock
{
    private readonly HashSet<RepoName> _held = [];

    /// <summary>Whether the repo's lock is held now.</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>True while a holder has it.</returns>
    public bool IsHeld(RepoName repo)
    {
        lock (_held)
        {
            return _held.Contains(repo);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IAsyncDisposable>> Take(RepoName repo, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        lock (_held)
        {
            return ValueTask.FromResult<Result<IAsyncDisposable>>(_held.Add(repo) ? new Held(this, repo) : CodeIndexErrors.IndexBusy.With("the index is locked"));
        }
    }

    private sealed class Held(FakeRepoIndexLock owner, RepoName repo) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                lock (owner._held)
                {
                    _ = owner._held.Remove(repo);
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
