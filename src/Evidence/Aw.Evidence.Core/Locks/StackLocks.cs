using Aw.Kernel;

namespace Aw.Evidence.Core.Locks;

/// <summary>
/// The lock order a browser run uses: the QA stack first (the wider lock), then the heavy-job lock. Failing to take the
/// second, by error or by exception, releases the first, so a partial failure never leaves a lock held.
/// </summary>
public static class StackLocks
{
    /// <summary>Takes both locks, widest first.</summary>
    /// <param name="locks">The machine's locks.</param>
    /// <param name="timeout">How long to wait for each.</param>
    /// <param name="cancellationToken">Cancels the waits.</param>
    /// <returns>One lease holding both (released in reverse order), or the error of the lock that stayed busy.</returns>
    public static async ValueTask<Result<ILockLease>> AcquireAsync(IMachineLocks locks, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stack = await locks.AcquireAsync(LockKind.QaStack, timeout, cancellationToken).ConfigureAwait(false);
        if (stack.IsError)
        {
            return stack.Error;
        }

        Result<ILockLease> heavy;
        try
        {
            heavy = await locks.AcquireAsync(LockKind.HeavyJob, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Any exception (a cancelled wait, an adapter failing outside its Result) releases the lock already taken.
            await stack.Value.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        if (heavy.IsError)
        {
            await stack.Value.DisposeAsync().ConfigureAwait(false);
            return heavy.Error;
        }

        return new CompositeLease(stack.Value, heavy.Value);
    }

    private sealed class CompositeLease(ILockLease outer, ILockLease inner) : ILockLease
    {
        public IReadOnlyList<LockKind> Held { get; } = [.. outer.Held, .. inner.Held];

        public async ValueTask DisposeAsync()
        {
            try
            {
                await inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await outer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
