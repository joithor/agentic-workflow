using System.Text.Json.Serialization;
using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// The limits file the launcher writes beside the plan and 'session wrap --limits' reads (sandbox spec §3.3; SB-3a). A sandboxed agent must never run
/// unwatched, so a file that cannot be read or names no unit refuses the start.
/// </summary>
/// <param name="Unit">The scope unit.</param>
/// <param name="WallSeconds">The unsuspended run time allowed, grace included; absent for an attended session.</param>
/// <param name="WallGraceSeconds">The grace inside the wall.</param>
/// <param name="ParkMaxSeconds">The longest single suspension the ceiling allows for.</param>
/// <param name="MemoryMaxBytes">The memory kill line, for the limit report.</param>
internal sealed record WrapLimits(
    string Unit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? WallSeconds,
    long WallGraceSeconds,
    long ParkMaxSeconds,
    long MemoryMaxBytes)
{
    /// <summary>Reads a limits file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The limits, or SND-REQ-001 when the file is unreadable, malformed or has no unit.</returns>
    internal static Result<WrapLimits> Read(string path) => throw new NotSupportedException();
}
