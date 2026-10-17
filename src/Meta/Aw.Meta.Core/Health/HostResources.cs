namespace Aw.Meta.Core.Health;

/// <summary>The host's memory and the state disk.</summary>
/// <param name="MemTotalKb">Total memory in kilobytes.</param>
/// <param name="MemAvailableKb">Available memory in kilobytes.</param>
/// <param name="SwapUsedKb">Swap in use in kilobytes.</param>
/// <param name="DiskFreeBytes">Free bytes on the state directory's disk.</param>
/// <param name="SwapTotalKb">Total swap in kilobytes; 0 when the host has none.</param>
public sealed record HostResources(long MemTotalKb, long MemAvailableKb, long SwapUsedKb, long DiskFreeBytes, long SwapTotalKb = 0)
{
    /// <summary>Swap in use as a percentage of the swap total (0 when the host has no swap).</summary>
    public int SwapUsedPercent => SwapTotalKb <= 0 ? 0 : (int)Math.Min(100, SwapUsedKb * 100 / SwapTotalKb);

    /// <summary>Available memory as a percentage of the total (100 when the total is unknown).</summary>
    public int AvailablePercent => MemTotalKb <= 0 ? 100 : (int)(MemAvailableKb * 100 / MemTotalKb);
}
