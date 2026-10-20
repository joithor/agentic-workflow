using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Core.Setup;

/// <summary>Where the managed Onyx stands, from its status.</summary>
/// <param name="State">Not set up, running, stopped or unknown.</param>
/// <param name="Unreachable">True when it runs but doesn't answer.</param>
/// <param name="BlockedByCode">The error code of the first failed preflight check (Docker, Compose, memory, disk or port), or null when every check passes.</param>
public sealed record OnyxFact(OnyxState State, bool Unreachable, string? BlockedByCode)
{
    /// <summary>The fact from Onyx's status: unreachable when it runs and doesn't answer, blocked by the first failed preflight check's code.</summary>
    /// <param name="report">The status.</param>
    /// <returns>The fact.</returns>
    public static OnyxFact From(OnyxStatusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new OnyxFact(
            report.State,
            Unreachable: report.State == OnyxState.Running && string.Equals(report.RunningVersion, "unreachable", StringComparison.Ordinal),
            report.Checks.FirstOrDefault(static c => c.State == CheckState.Fail && c.Problem is not null)?.Problem?.Code);
    }
}
