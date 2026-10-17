using System.Diagnostics;
using System.Globalization;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>The descendants of a process this runner started, read from /proc (Linux), and a polite stop for them.</summary>
internal static class ProcessTree
{
    /// <summary>The process and everything below it now.</summary>
    /// <param name="root">The process id.</param>
    /// <returns>The pids, the root first; empty when /proc can't be read (not Linux).</returns>
    internal static IReadOnlyList<int> Of(int root)
    {
        var parents = new Dictionary<int, List<int>>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && ParentOf(directory) is { } parent)
                {
                    if (!parents.TryGetValue(parent, out var children))
                    {
                        parents[parent] = children = [];
                    }

                    children.Add(pid);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var tree = new List<int> { root };
        for (var i = 0; i < tree.Count; i++)
        {
            if (parents.TryGetValue(tree[i], out var below))
            {
                tree.AddRange(below);
            }
        }

        return tree;
    }

    /// <summary>Sends SIGTERM to each pid.</summary>
    /// <param name="pids">The pids this runner started.</param>
    /// <returns>Whether the signal was sent.</returns>
    internal static async Task<bool> Terminate(IReadOnlyList<int> pids)
    {
        var info = new ProcessStartInfo("kill") { UseShellExecute = false };
        info.ArgumentList.Add("-TERM");
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

    // The parent pid is the field after the command name, which is in parentheses and may itself contain spaces and parentheses.
    private static int? ParentOf(string directory)
    {
        try
        {
            var stat = File.ReadAllText(Path.Combine(directory, "stat"));
            var after = stat.LastIndexOf(')');
            var fields = after < 0 ? [] : stat[(after + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent) ? parent : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
