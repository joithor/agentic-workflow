namespace Aw.Meta.Core.Config;

/// <summary>The memory and disk thresholds the watch and the builders advice use; every field is optional.</summary>
/// <param name="DiskFloorGb">Disk is low below this many free gigabytes on the state disk.</param>
/// <param name="MemFloorGb">Memory is low below this many available gigabytes.</param>
/// <param name="SwapHighPct">Memory is low at or above this share of swap in use (a host with no swap never trips it).</param>
public sealed record ResourcesConfig(int DiskFloorGb = 30, int MemFloorGb = 8, int SwapHighPct = 90);
