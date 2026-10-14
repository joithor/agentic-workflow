using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>A process table in memory: each pid's parent, name and executable. Kept honest by <see cref="ProcessAncestryContract"/>.</summary>
public sealed class FakeProcessAncestry : IProcessAncestry
{
    private readonly Dictionary<int, (int Parent, string Name, string? Executable)> _table = new() { [1] = (0, "launchd", "/sbin/launchd") };

    /// <summary>
    /// A table where <paramref name="pid"/> runs under <paramref name="names"/>, nearest first, then pid 1 ('launchd').
    /// The ancestors get pids 1000, 1001, … in that order, and no executable path.
    /// </summary>
    /// <param name="pid">The process at the bottom (usually this test process).</param>
    /// <param name="names">Its ancestors' names, nearest first.</param>
    /// <returns>The table.</returns>
    public static FakeProcessAncestry Under(int pid, params string[] names) =>
        UnderProcesses(pid, [.. names.Select(n => (n, (string?)null))]);

    /// <summary>
    /// A table where <paramref name="pid"/> runs under <paramref name="ancestors"/> (name and executable path), nearest
    /// first, then pid 1 ('launchd'). The ancestors get pids 1000, 1001, … in that order.
    /// </summary>
    /// <param name="pid">The process at the bottom (usually this test process).</param>
    /// <param name="ancestors">Its ancestors, nearest first.</param>
    /// <returns>The table.</returns>
    public static FakeProcessAncestry UnderProcesses(int pid, params (string Name, string? Executable)[] ancestors)
    {
        var fake = new FakeProcessAncestry();
        fake._table[pid] = (ancestors.Length == 0 ? 1 : 1000, "self", null);
        for (var i = 0; i < ancestors.Length; i++)
        {
            fake._table[1000 + i] = (i == ancestors.Length - 1 ? 1 : 1001 + i, ancestors[i].Name, ancestors[i].Executable);
        }

        return fake;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProcessAncestor> Ancestors(int pid, int max)
    {
        var found = new List<ProcessAncestor>();
        var current = pid;
        while (found.Count < max && current != 1 && _table.TryGetValue(current, out var entry) && _table.TryGetValue(entry.Parent, out var parent))
        {
            found.Add(new ProcessAncestor(entry.Parent, parent.Name) { Executable = parent.Executable });
            current = entry.Parent;
        }

        return found;
    }
}
