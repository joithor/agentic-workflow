using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's checks of the services Sindri runs beside or reads from, after its own lease, ledger, profile and api:
/// the Temporal dev server ('temporal', engine spec rev 8 §0) and the onyx context source ('context:onyx').
/// </summary>
/// <param name="temporal">The Temporal dev server.</param>
/// <param name="onyx">The onyx check.</param>
internal sealed class ServiceChecks(ITemporalServer temporal, OnyxDoctor onyx)
{
    /// <summary>The checks, in the order doctor prints them.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The checks.</returns>
    internal async ValueTask<IReadOnlyList<DoctorCheck>> Check(LoadedProfile? approved, CancellationToken cancellationToken) =>
    [
        DoctorRules.Temporal(await temporal.Inspect(cancellationToken).ConfigureAwait(false)),
        await onyx.Check(approved, cancellationToken).ConfigureAwait(false),
    ];
}
