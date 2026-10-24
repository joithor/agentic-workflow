using System.Globalization;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Health;

/// <summary>How many builders each lane should run now, given the disk and memory the host has.</summary>
/// <param name="PerLane">The builders per lane to run now (1 at least; <see cref="int.MaxValue"/> when nothing caps it and worktrees take no disk).</param>
/// <param name="MaxPerLane">The configured most; null when the team set none (the disk and memory are the only limits).</param>
/// <param name="DiskFreeGb">Free gigabytes on the state disk.</param>
/// <param name="MemAvailableGb">Available gigabytes of memory.</param>
/// <param name="Limit">What cut the number below the configured most, or what disk is short of at one builder; null when nothing limits it.</param>
public sealed record BuilderAdvice(int PerLane, int? MaxPerLane, long DiskFreeGb, long MemAvailableGb, string? Limit)
{
    /// <summary>The limit text when the disk cannot hold the floor plus the builders' worktrees.</summary>
    public const string DiskLimit = "disk below floor+headroom";

    /// <summary>The limit text when memory is low.</summary>
    public const string MemoryLimit = "memory low";

    /// <summary>
    /// The configured maximum, reduced while free disk is under the floor plus lanes x builders x worktree size, and to 1 while
    /// memory is low (<see cref="ResourceRules.MemoryLow"/>: under its floor; swap does not count).
    /// </summary>
    /// <param name="host">The host's figures.</param>
    /// <param name="limits">The disk and memory floors.</param>
    /// <param name="builders">The maximum per lane and the worktree size.</param>
    /// <param name="lanes">The lanes that run builders (at least 1 is assumed).</param>
    /// <returns>The advice.</returns>
    public static BuilderAdvice Compute(HostResources host, ResourcesConfig limits, BuildersConfig builders, int lanes)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(builders);
        var max = builders.MaxPerLane is { } configured ? Math.Max(1, configured) : (int?)null;
        var diskGb = ResourceRules.DiskFreeGb(host);
        var memGb = ResourceRules.MemAvailableGb(host);
        if (ResourceRules.MemoryLow(host, limits))
        {
            return new BuilderAdvice(1, max, diskGb, memGb, MemoryLimit);
        }

        var perBuilder = (long)Math.Max(1, lanes) * builders.WorktreeGb;
        var fit = perBuilder <= 0 ? int.MaxValue : (int)Math.Clamp((diskGb - limits.DiskFloorGb) / perBuilder, 1, int.MaxValue - 1);
        var diskCuts = (max is { } most && fit < most) || diskGb < limits.DiskFloorGb + perBuilder;
        return new BuilderAdvice(Math.Min(fit, max ?? int.MaxValue), max, diskGb, memGb, diskCuts ? DiskLimit : null);
    }

    /// <summary>The one-line form: "builders: 4 per lane (disk 132 GB free, mem 12 GB available)", or the limit in the brackets.</summary>
    /// <returns>The line.</returns>
    public string Line() => string.Create(
        CultureInfo.InvariantCulture,
        $"builders: {(PerLane == int.MaxValue ? "no limit" : PerLane)} per lane ({Limit ?? $"disk {DiskFreeGb} GB free, mem {MemAvailableGb} GB available"})");
}
