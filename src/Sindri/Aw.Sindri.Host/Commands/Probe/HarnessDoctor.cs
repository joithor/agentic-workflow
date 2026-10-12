using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// 'sindri doctor''s harness lines (engine spec §6.2): re-probes the harness this doctor runs in, compares every other
/// probed harness's installed version with the pinned probe, and warns (never 'ok') on a mismatch or a pending probe.
/// </summary>
internal static class HarnessDoctor
{
    /// <summary>One line per pinned agent harness, and per pending human terminal.</summary>
    /// <param name="pinned">The pinned probe.</param>
    /// <param name="live">A probe of this process.</param>
    /// <param name="versions">Reads installed versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The checks.</returns>
    internal static ValueTask<IReadOnlyList<DoctorCheck>> Checks(
        IReadOnlyList<PinnedHarness> pinned, HarnessProbeReport live, IHarnessVersions versions, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P2 unit (e): red");
}
