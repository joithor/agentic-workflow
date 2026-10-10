using System.Collections;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The task dependency graph: unknown dependencies, cycles, and which tasks a dependency orders.</summary>
internal sealed class TaskGraph
{
    private readonly Dictionary<TaskId, IReadOnlyList<TaskId>> _deps;
    private readonly Dictionary<TaskId, int> _position = [];
    private readonly Dictionary<TaskId, BitArray> _reach = [];

    internal TaskGraph(IReadOnlyList<PacketTask> tasks)
    {
        _deps = tasks.GroupBy(static t => t.Id).ToDictionary(static g => g.Key, static g => (IReadOnlyList<TaskId>)[.. g.SelectMany(static t => t.Deps)]);
        Ids = [.. _deps.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal)];
        for (var i = 0; i < Ids.Count; i++)
        {
            _position[Ids[i]] = i;
        }
    }

    internal IReadOnlyList<TaskId> Ids { get; }

    /// <summary>
    /// Each strongly connected group once (Tarjan), in order of its smallest id, naming every task in it and one
    /// cycle through it. Every task that is on any cycle is named, even where the group holds several cycles.
    /// </summary>
    internal IReadOnlyList<TaskCycle> Cycles()
    {
        var state = new Tarjan(_deps);
        foreach (var id in Ids)
        {
            state.Visit(id);
        }

        return [.. state.Groups
            .Where(g => g.Count > 1 || _deps[g[0]].Contains(g[0]))
            .Select(g => new TaskCycle([.. g.OrderBy(static id => id.Value, StringComparer.Ordinal)], PathThrough(g)))
            .OrderBy(static c => c.Members[0].Value, StringComparer.Ordinal)];
    }

    /// <summary>True when a chain of dependencies orders <paramref name="a"/> and <paramref name="b"/> either way.</summary>
    internal bool Ordered(TaskId a, TaskId b) =>
        _position.TryGetValue(a, out var pa) && _position.TryGetValue(b, out var pb) && (Reach(a)[pb] || Reach(b)[pa]);

    // One cycle through the group, from its smallest id: a depth-first walk inside the group (deps in declared order),
    // iterative so a long chain can't overflow the stack. Every member of a strongly connected group reaches the start.
    private List<TaskId> PathThrough(List<TaskId> group)
    {
        var members = group.ToHashSet();
        var start = group.MinBy(static id => id.Value, StringComparer.Ordinal);
        var path = new List<TaskId> { start };
        var seen = new HashSet<TaskId> { start };
        var next = new Stack<int>();
        next.Push(0);
        while (next.TryPop(out var at))
        {
            var deps = _deps[path[^1]];
            if (at >= deps.Count)
            {
                path.RemoveAt(path.Count - 1);
                continue;
            }

            next.Push(at + 1);
            var dep = deps[at];
            if (dep == start)
            {
                path.Add(start);
                return path;
            }

            if (members.Contains(dep) && seen.Add(dep))
            {
                path.Add(dep);
                next.Push(0);
            }
        }

        return [start, start];
    }

    // Every task 'from' waits on, directly or not, as one bit per task (memory n²/8 bytes at most), computed once.
    private BitArray Reach(TaskId from)
    {
        if (_reach.TryGetValue(from, out var known))
        {
            return known;
        }

        var seen = new BitArray(Ids.Count);
        var stack = new Stack<TaskId>(_deps[from]);
        while (stack.TryPop(out var next))
        {
            if (_position.TryGetValue(next, out var at) && !seen[at])
            {
                seen[at] = true;
                foreach (var dep in _deps[next])
                {
                    stack.Push(dep);
                }
            }
        }

        _reach[from] = seen;
        return seen;
    }
}
