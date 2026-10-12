using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>A process table in memory: each pid's parent and name. Kept honest by <see cref="ProcessAncestryContract"/>.</summary>
public sealed class FakeProcessAncestry : IProcessAncestry
{
    private readonly Dictionary<int, (int Parent, string Name)> _table = new() { [1] = (0, "launchd") };

    /// <summary>
    /// A table where <paramref name="pid"/> runs under <paramref name="names"/>, nearest first, then pid 1 ('launchd').
    /// The ancestors get pids 1000, 1001, … in that order.
    /// </summary>
    /// <param name="pid">The process at the bottom (usually this test process).</param>
    /// <param name="names">Its ancestors' names, nearest first.</param>
    /// <returns>The table.</returns>
    public static FakeProcessAncestry Under(int pid, params string[] names)
    {
        var fake = new FakeProcessAncestry();
        fake._table[pid] = (names.Length == 0 ? 1 : 1000, "self");
        for (var i = 0; i < names.Length; i++)
        {
            fake._table[1000 + i] = (i == names.Length - 1 ? 1 : 1001 + i, names[i]);
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
            found.Add(new ProcessAncestor(entry.Parent, parent.Name));
            current = entry.Parent;
        }

        return found;
    }
}
