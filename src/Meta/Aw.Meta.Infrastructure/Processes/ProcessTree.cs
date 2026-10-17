using System.Diagnostics;
using System.Globalization;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>The descendants of a process this runner started, read from /proc (Linux), and a polite stop for them.</summary>
internal static class ProcessTree
{
    /// <summary>The process and everything below it now.</summary>
    /// <param name="root">The process id.</param>
    /// <returns>The pids, the root first; empty when /proc can't be read (not Linux).</returns>
    internal static IReadOnlyList<int> Of(int root) => [.. Snapshot(root).Select(p => p.Pid)];

    /// <summary>The process and everything below it now, each with its /proc start time so a later signal can tell it from a reused pid.</summary>
    /// <param name="root">The process id.</param>
    /// <returns>The processes, the root first; empty when /proc can't be read (not Linux).</returns>
    internal static IReadOnlyList<ProcessRef> Snapshot(int root)
    {
        var parents = new Dictionary<int, List<ProcessRef>>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && Read(directory) is { } stat)
                {
                    if (!parents.TryGetValue(stat.Parent, out var children))
                    {
                        parents[stat.Parent] = children = [];
                    }

                    children.Add(new ProcessRef(pid, stat.Start));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var tree = new List<ProcessRef> { new(root, Start(root) ?? string.Empty) };
        for (var i = 0; i < tree.Count; i++)
        {
            if (parents.TryGetValue(tree[i].Pid, out var below))
            {
                tree.AddRange(below);
            }
        }

        return tree;
    }

    /// <summary>Whether the process still runs: its pid exists, its start time is the recorded one, and it is not a zombie.</summary>
    /// <param name="process">The recorded process.</param>
    /// <returns>True when it is the same live process.</returns>
    internal static bool IsAlive(ProcessRef process) =>
        Read(Path.Combine("/proc", process.Pid.ToString(CultureInfo.InvariantCulture))) is { } stat && stat.Start == process.Start && stat.State != 'Z';

    /// <summary>Whether a pid exists in /proc (any state).</summary>
    /// <param name="pid">The pid.</param>
    /// <returns>True when /proc has it.</returns>
    internal static bool Exists(int pid) => Directory.Exists("/proc/" + pid.ToString(CultureInfo.InvariantCulture));

    /// <summary>Sends SIGTERM to each pid.</summary>
    /// <param name="pids">The pids this runner started.</param>
    /// <returns>Whether the signal was sent.</returns>
    internal static Task<bool> Terminate(IReadOnlyList<int> pids) => Signal("TERM", pids);

    /// <summary>Sends SIGKILL to each pid.</summary>
    /// <param name="pids">The pids this runner started.</param>
    /// <returns>Whether the signal was sent.</returns>
    internal static Task<bool> Kill(IReadOnlyList<int> pids) => Signal("KILL", pids);

    private static async Task<bool> Signal(string signal, IReadOnlyList<int> pids)
    {
        if (pids.Count == 0)
        {
            return true;
        }

        var info = new ProcessStartInfo("kill") { UseShellExecute = false };
        info.ArgumentList.Add("-" + signal);
        info.ArgumentList.Add("--");
        foreach (var pid in pids)
        {
            info.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        }

        try
        {
            using var kill = Process.Start(info);
            if (kill is null)
            {
                return false;
            }

            await kill.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string? Start(int pid) => Read(Path.Combine("/proc", pid.ToString(CultureInfo.InvariantCulture)))?.Start;

    // State, parent pid and start time are the fields after the command name, which is in parentheses and may itself contain spaces and parentheses.
    private static StatLine? Read(string directory)
    {
        try
        {
            var stat = File.ReadAllText(Path.Combine(directory, "stat"));
            var after = stat.LastIndexOf(')');
            var fields = after < 0 ? [] : stat[(after + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 19 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent) ? new StatLine(fields[0][0], parent, fields[19]) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private readonly record struct StatLine(char State, int Parent, string Start);
}
