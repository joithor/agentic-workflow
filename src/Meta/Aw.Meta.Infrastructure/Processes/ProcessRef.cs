namespace Aw.Meta.Infrastructure.Processes;

/// <summary>A process as /proc showed it: its pid and start time (clock ticks since boot), which together name one process.</summary>
/// <param name="Pid">The pid.</param>
/// <param name="Start">The start time field of /proc/&lt;pid&gt;/stat.</param>
internal readonly record struct ProcessRef(int Pid, string Start);
