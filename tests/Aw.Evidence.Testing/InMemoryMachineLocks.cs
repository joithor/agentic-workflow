using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>
/// In-process IMachineLocks: one semaphore per kind, shared by every holder of this instance. The QA-stack lock has one
/// slot; the heavy-job lock has <paramref name="heavyJobSlots"/> (2 by default, as on the machine). It deliberately
/// leaves out the directory adapter's own behaviour: the 'waiting on lock' notice, owner files and slot directories
/// (DirectoryLocksTests covers those), so no test may depend on them through this fake.
/// </summary>
/// <param name="heavyJobSlots">How many holders the heavy-job lock admits at once.</param>
public sealed class InMemoryMachineLocks(int heavyJobSlots = 2) : IMachineLocks
{
    private readonly Dictionary<LockKind, (SemaphoreSlim Slots, int Count)> _locks = new()
    {
        [LockKind.QaStack] = (new(1, 1), 1),
        [LockKind.HeavyJob] = (new(heavyJobSlots, heavyJobSlots), heavyJobSlots),
    };

    private readonly List<string> _events = [];

    /// <summary>Kinds whose acquire always fails as busy (to test partial failures).</summary>
    public ISet<LockKind> AlwaysBusy { get; } = new HashSet<LockKind>();

    /// <summary>Kinds whose acquire throws an IOException (an adapter failing outside its Result, such as a closed stderr).</summary>
    public ISet<LockKind> Throws { get; } = new HashSet<LockKind>();

    /// <summary>Kinds whose acquire fails with AW-EVIDENCE-011 (a lock directory that cannot be made): an error that is not busy.</summary>
    public ISet<LockKind> Fails { get; } = new HashSet<LockKind>();

    /// <summary>'acquire QaStack', 'release QaStack', … in order.</summary>
    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_events)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>Whether any holder has <paramref name="kind"/> (any slot of it) now.</summary>
    /// <param name="kind">The lock.</param>
    /// <returns>Whether it is held.</returns>
    public bool IsHeld(LockKind kind) => _locks[kind].Slots.CurrentCount < _locks[kind].Count;

    /// <inheritdoc/>
    public async ValueTask<Result<ILockLease>> AcquireAsync(LockKind kind, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Throws.Contains(kind))
        {
            throw new IOException($"{kind}: the lock adapter failed");
        }

        if (Fails.Contains(kind))
        {
            return EvidenceErrors.FileUnavailable.With($"{kind}: the lock directory could not be made");
        }

        if (AlwaysBusy.Contains(kind) || !await _locks[kind].Slots.WaitAsync(timeout, cancellationToken))
        {
            return EvidenceErrors.LockBusy.With(string.Create(CultureInfo.InvariantCulture, $"{kind} stayed busy for {timeout.TotalSeconds:0.###} s"));
        }

        Log($"acquire {kind}");
        return new Lease(this, kind);
    }

    private void Log(string entry)
    {
        lock (_events)
        {
            _events.Add(entry);
        }
    }

    private sealed class Lease(InMemoryMachineLocks owner, LockKind kind) : ILockLease
    {
        private int _released;

        public IReadOnlyList<LockKind> Held { get; } = [kind];

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Log($"release {kind}");
                _ = owner._locks[kind].Slots.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
