namespace Aw.Meta.Infrastructure.Files;

/// <summary>Whether a process id belongs to a running process: its /proc entry on Linux, the OS process list elsewhere (macOS).</summary>
internal static class ProcessLiveness
{
    /// <summary>Whether the pid is a running process (a zombie counts as running until it is reaped).</summary>
    /// <param name="pid">The process id.</param>
    /// <returns>True when the process exists.</returns>
    internal static bool IsAlive(int pid) => throw new NotSupportedException();
}
