namespace Aw.Meta.Core.Health;

/// <summary>The host at one moment.</summary>
/// <param name="Processes">Every live process.</param>
/// <param name="MemAvailableKb">Available memory in kilobytes.</param>
/// <param name="SwapUsedKb">Swap in use in kilobytes.</param>
/// <param name="DiskFreeBytes">Free bytes on the state directory's disk.</param>
public sealed record HostSnapshot(IReadOnlyList<ProcessInfo> Processes, long MemAvailableKb, long SwapUsedKb, long DiskFreeBytes);
