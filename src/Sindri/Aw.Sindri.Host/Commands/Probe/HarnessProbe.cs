using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// The agent-variable probe (engine spec §6.2): takes a <see cref="HarnessProbeReport"/> in this process, and compares a
/// live report with the pinned one ('doctor' warns, never 'ok', when a Codex or Cursor version differs). P2 contract:
/// shape only, owned by unit (e).
/// </summary>
internal static class HarnessProbe
{
    /// <summary>Probes this process.</summary>
    /// <param name="harness">The harness name the owner gave.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="ancestry">This process's ancestry.</param>
    /// <param name="interactive">Whether stdin and stdout are a terminal.</param>
    /// <returns>The report.</returns>
    internal static HarnessProbeReport Take(string harness, Func<string, string?> environment, IProcessAncestry ancestry, bool interactive)
    {
        ArgumentNullException.ThrowIfNull(harness);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(ancestry);
        _ = interactive;
        throw new NotSupportedException(Pending);
    }

    /// <summary>What 'doctor --probe' answers until unit (e) ships it.</summary>
    internal const string Pending =
        "doctor --probe arrives with engine P2 unit (e): it records the agent variables and process ancestry inside a harness (fix: run sindri doctor)";
}
