using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// The agent-variable probe (engine spec §6.2): takes a <see cref="HarnessProbeReport"/> in this process. Names only,
/// never a value, so no secret is recorded.
/// </summary>
internal static class HarnessProbe
{
    /// <summary>How many ancestors the probe reads: enough to reach launchd or init.</summary>
    internal const int Depth = 64;

    /// <summary>Probes this process.</summary>
    /// <param name="input">What was read from it.</param>
    /// <param name="versions">Reads a harness's version when its executable path doesn't carry one.</param>
    /// <param name="cancellationToken">Cancels the version read.</param>
    /// <returns>The report.</returns>
    internal static ValueTask<HarnessProbeReport> Take(ProbeInput input, IHarnessVersions versions, CancellationToken cancellationToken) =>
        throw new NotSupportedException("P2 unit (e): red");
}
