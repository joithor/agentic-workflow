using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// Makes a scheduled run and a manual run of the same service exclusive, and shares the last outcome between them (API P6, plan 1.5): the scheduler's
/// 'idle after ok writes nothing' rule must see a manual run's outcome (G7). It replaces the scheduler's private in-flight and last-outcome maps.
/// </summary>
public sealed class ServiceRunGuard
{
    /// <summary>Claims <paramref name="service"/> for one run.</summary>
    /// <param name="service">The service.</param>
    /// <param name="claim">Releases the claim when disposed (twice is harmless).</param>
    /// <returns>False when a run of that service is in flight.</returns>
    public bool TryClaim(ServiceName service, out IDisposable claim) => throw new NotSupportedException("red until API P6 unit D");

    /// <summary>The last recorded outcome of <paramref name="service"/>, or null when none is known yet.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The outcome, if known.</returns>
    public ServiceOutcome? LastOutcome(ServiceName service) => throw new NotSupportedException("red until API P6 unit D");

    /// <summary>Records how a run of <paramref name="service"/> ended.</summary>
    /// <param name="service">The service.</param>
    /// <param name="outcome">The outcome.</param>
    public void Record(ServiceName service, ServiceOutcome outcome) => throw new NotSupportedException("red until API P6 unit D");

    /// <summary>Forgets the last outcome of <paramref name="service"/> (what the ledger holds is unknown after a failed write).</summary>
    /// <param name="service">The service.</param>
    public void Forget(ServiceName service) => throw new NotSupportedException("red until API P6 unit D");
}
