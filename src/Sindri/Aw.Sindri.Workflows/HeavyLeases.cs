using System.Collections.Concurrent;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The heavy-job slots this worker holds for instances (LN-WF3), by instance address: a lease lives in memory, in the process that took
/// it, because the lock is a directory owned by a process id. A worker that stops disposes them all, which is a lost lease; the instance
/// takes a slot again before its next Step off the desk.
/// </summary>
internal sealed class HeavyLeases : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _held = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _revoked = new(StringComparer.Ordinal);

    /// <summary>Whether a slot is held for <paramref name="address"/>.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>True when one is.</returns>
    public bool Holds(string address) => _held.ContainsKey(address);

    /// <summary>Keeps <paramref name="lease"/> for <paramref name="address"/>; a slot already held for it, or a take a release already covered, gives the new one back.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="attempt">The take the slot came from.</param>
    /// <param name="lease">The slot just taken.</param>
    /// <returns>Done.</returns>
    public async ValueTask Add(string address, int attempt, IAsyncDisposable lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if ((_revoked.TryGetValue(address, out var covered) && attempt <= covered) || !_held.TryAdd(address, lease))
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Gives back the slot held for <paramref name="address"/>, if any, and covers every take up to <paramref name="attempt"/>.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="attempt">The latest take number the instance started.</param>
    /// <returns>Done.</returns>
    public async ValueTask Release(string address, int attempt)
    {
        _ = _revoked.AddOrUpdate(address, static (_, latest) => latest, static (_, was, latest) => Math.Max(was, latest), attempt);
        if (_held.TryRemove(address, out var lease))
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var address in _held.Keys)
        {
            if (_held.TryRemove(address, out var lease))
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
