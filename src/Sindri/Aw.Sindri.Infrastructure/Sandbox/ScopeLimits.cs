using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The numbers one sandboxed session's cgroup scope is started with (sandbox spec §3.3). The profile's values win; a key left out takes
/// <see cref="SandboxDefaults"/>.
/// </summary>
/// <param name="MemoryMaxBytes">The kill line: 'auto' is a fifth of RAM, at most 12 GiB.</param>
/// <param name="MemoryHighBytes">The throttle line: 80% of the max.</param>
/// <param name="SliceTotalBytes">The parent slice's memory cap for all sessions together.</param>
/// <param name="CpuQuotaPercent">The CPU quota in percent of one core (all cores at the per-core share).</param>
/// <param name="CpuWeight">The CPU weight against other work.</param>
/// <param name="TasksMax">Processes per session.</param>
/// <param name="Wall">The unsuspended run time allowed, grace included; null for an attended session (no wall).</param>
/// <param name="WallGrace">The grace already inside <paramref name="Wall"/>.</param>
/// <param name="ParkMax">The longest single suspension the scope's loose ceiling allows for.</param>
internal sealed record ScopeLimits(long MemoryMaxBytes, long MemoryHighBytes, long SliceTotalBytes, int CpuQuotaPercent, int CpuWeight, int TasksMax,
    TimeSpan? Wall, TimeSpan WallGrace, TimeSpan ParkMax)
{
    internal const long Gib = 1024L * 1024 * 1024;

    /// <summary>Resolves the limits for one launch.</summary>
    /// <param name="profile">The profile's 'sandbox.limits', or null.</param>
    /// <param name="box">The box's RAM and cores.</param>
    /// <param name="stepTimeout">The Step's timeout, or null when it has none (4 h is then the wall before the grace).</param>
    /// <param name="longMax">The longest wall any Step gets before the grace.</param>
    /// <param name="attended">True for a session the owner opened: no wall.</param>
    /// <returns>The limits.</returns>
    internal static ScopeLimits From(SandboxLimits? profile, BoxFacts box, TimeSpan? stepTimeout, TimeSpan longMax, bool attended) =>
        throw new NotSupportedException();
}
