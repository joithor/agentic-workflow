using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Health;

/// <summary>The watch's disk and memory rules over the configured thresholds.</summary>
public static class ResourceRules
{
    private const long BytesPerGb = 1024L * 1024 * 1024;
    private const long KbPerGb = 1024L * 1024;

    /// <summary>Whether free disk is under the floor.</summary>
    /// <param name="host">The host's figures.</param>
    /// <param name="limits">The thresholds.</param>
    /// <returns>True when disk is low.</returns>
    public static bool DiskLow(HostResources host, ResourcesConfig limits)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(limits);
        return host.DiskFreeBytes < limits.DiskFloorGb * BytesPerGb;
    }

    /// <summary>Whether available memory is under the floor, or swap is nearly all in use.</summary>
    /// <param name="host">The host's figures.</param>
    /// <param name="limits">The thresholds.</param>
    /// <returns>True when memory is low.</returns>
    public static bool MemoryLow(HostResources host, ResourcesConfig limits)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(limits);
        return host.MemAvailableKb < limits.MemFloorGb * KbPerGb || (host.SwapTotalKb > 0 && host.SwapUsedPercent >= limits.SwapHighPct);
    }

    /// <summary>Whole gigabytes of free disk.</summary>
    /// <param name="host">The host's figures.</param>
    /// <returns>The free gigabytes, rounded down.</returns>
    public static long DiskFreeGb(HostResources host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.DiskFreeBytes / BytesPerGb;
    }

    /// <summary>Whole gigabytes of available memory.</summary>
    /// <param name="host">The host's figures.</param>
    /// <returns>The available gigabytes, rounded down.</returns>
    public static long MemAvailableGb(HostResources host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.MemAvailableKb / KbPerGb;
    }
}
