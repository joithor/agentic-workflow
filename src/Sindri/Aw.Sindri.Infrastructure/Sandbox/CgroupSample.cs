namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One read of a scope's accounting files.</summary>
/// <param name="OomKills">'oom_kill' in memory.events: processes the kernel killed for memory.</param>
/// <param name="MemoryCurrent">memory.current, in bytes.</param>
/// <param name="MemoryPeak">memory.peak, in bytes; 0 where the kernel does not offer it.</param>
/// <param name="Pids">pids.current.</param>
/// <param name="CpuMicroseconds">'usage_usec' in cpu.stat.</param>
internal sealed record CgroupSample(long OomKills, long MemoryCurrent, long MemoryPeak, int Pids, long CpuMicroseconds);
