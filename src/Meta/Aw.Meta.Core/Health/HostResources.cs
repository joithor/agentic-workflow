namespace Aw.Meta.Core.Health;

/// <summary>The host's memory and the state disk.</summary>
/// <param name="MemTotalKb">Total memory in kilobytes.</param>
/// <param name="MemAvailableKb">Available memory in kilobytes.</param>
/// <param name="SwapUsedKb">Swap in use in kilobytes.</param>
/// <param name="DiskFreeBytes">Free bytes on the state directory's disk.</param>
public sealed record HostResources(long MemTotalKb, long MemAvailableKb, long SwapUsedKb, long DiskFreeBytes)
{
    /// <summary>Available memory as a percentage of the total (100 when the total is unknown).</summary>
    public int AvailablePercent => MemTotalKb <= 0 ? 100 : (int)(MemAvailableKb * 100 / MemTotalKb);
}
