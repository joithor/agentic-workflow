namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The uid this process runs as, from /proc/self/status (the real uid, as the collector reads other processes').</summary>
internal static class OwnUid
{
    /// <summary>The uid, or -1 when it can't be read (no process then counts as ours, and the collector reports proc-unreadable).</summary>
    internal static int Read() => OperatingSystem.IsLinux() ? ProcFiles.RealUid("/proc/self/status") ?? -1 : -1;
}
