using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// Makes a scheduled run and a manual run of the same service exclusive, and shares the last outcome between them (API P6, plan 1.5): the scheduler's
/// 'idle after ok writes nothing' rule must see a manual run's outcome (G7). It replaces the scheduler's private in-flight and last-outcome maps.
/// </summary>
public sealed class ServiceRunGuard
{
    private readonly Lock _gate = new();
    private readonly HashSet<ServiceName> _claimed = [];
    private readonly Dictionary<ServiceName, ServiceOutcome> _last = [];

    /// <summary>Claims <paramref name="service"/> for one run.</summary>
    /// <param name="service">The service.</param>
    /// <param name="claim">Releases the claim when disposed (twice is harmless).</param>
    /// <returns>False when a run of that service is in flight.</returns>
    public bool TryClaim(ServiceName service, out IDisposable claim)
    {
        lock (_gate)
        {
            if (!_claimed.Add(service))
            {
                claim = NoClaim.Instance;
                return false;
            }
        }

        claim = new Claim(this, service);
        return true;
    }

    /// <summary>The last recorded outcome of <paramref name="service"/>, or null when none is known yet.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The outcome, if known.</returns>
    public ServiceOutcome? LastOutcome(ServiceName service)
    {
        lock (_gate)
        {
            return _last.TryGetValue(service, out var outcome) ? outcome : null;
        }
    }

    /// <summary>Records how a run of <paramref name="service"/> ended.</summary>
    /// <param name="service">The service.</param>
    /// <param name="outcome">The outcome.</param>
    public void Record(ServiceName service, ServiceOutcome outcome)
    {
        lock (_gate)
        {
            _last[service] = outcome;
        }
    }

    /// <summary>Forgets the last outcome of <paramref name="service"/> (what the ledger holds is unknown after a failed write).</summary>
    /// <param name="service">The service.</param>
    public void Forget(ServiceName service)
    {
        lock (_gate)
        {
            _ = _last.Remove(service);
        }
    }

    private void Release(ServiceName service)
    {
        lock (_gate)
        {
            _ = _claimed.Remove(service);
        }
    }

    private sealed class Claim(ServiceRunGuard guard, ServiceName service) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                guard.Release(service);
            }
        }
    }

    private sealed class NoClaim : IDisposable
    {
        public static NoClaim Instance { get; } = new();

        public void Dispose()
        {
            // a refused claim holds nothing to release
        }
    }
}
