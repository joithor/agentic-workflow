using System.Collections.Concurrent;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>
/// The per-repo index lock inside one process: a one-slot semaphore per repo (async waiters are served in arrival order, so an
/// embed run that takes the lock chunk after chunk cannot starve a build that is waiting). One Sindri process owns the index
/// directory, so no file lock is needed.
/// </summary>
internal sealed class InProcessRepoIndexLock : IRepoIndexLock
{
    private readonly ConcurrentDictionary<RepoName, SemaphoreSlim> _slots = new();

    public async ValueTask<Result<IAsyncDisposable>> Take(RepoName repo, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var slot = _slots.GetOrAdd(repo, static _ => new SemaphoreSlim(1, 1));
        if (!await slot.WaitAsync(maxWait, cancellationToken).ConfigureAwait(false))
        {
            return CodeIndexErrors.IndexBusy.With($"the index of '{repo.Value}' stayed locked");
        }

        return new Held(slot);
    }

    private sealed class Held(SemaphoreSlim slot) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _ = slot.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
