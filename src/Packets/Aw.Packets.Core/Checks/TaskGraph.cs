using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The task dependency graph: unknown dependencies, cycles, and which tasks a dependency orders.</summary>
internal sealed class TaskGraph
{
    private readonly Dictionary<TaskId, IReadOnlyList<TaskId>> _deps;
    private readonly Dictionary<TaskId, HashSet<TaskId>> _reach = [];

    internal TaskGraph(IReadOnlyList<PacketTask> tasks)
    {
        _deps = tasks.GroupBy(static t => t.Id).ToDictionary(static g => g.Key, static g => (IReadOnlyList<TaskId>)[.. g.SelectMany(static t => t.Deps)]);
        Ids = [.. _deps.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal)];
    }

    internal IReadOnlyList<TaskId> Ids { get; }

    /// <summary>Each cycle once, as ids from its smallest id back to it: 't1 -> t3 -> t2 -> t1'.</summary>
    internal IReadOnlyList<IReadOnlyList<TaskId>> Cycles()
    {
        var found = new SortedDictionary<string, IReadOnlyList<TaskId>>(StringComparer.Ordinal);
        var done = new HashSet<TaskId>();
        foreach (var start in Ids)
        {
            Walk(start, [], done, found);
        }

        return [.. found.Values];
    }

    /// <summary>True when a chain of dependencies orders <paramref name="a"/> and <paramref name="b"/> either way.</summary>
    internal bool Ordered(TaskId a, TaskId b) => Reach(a).Contains(b) || Reach(b).Contains(a);

    private void Walk(TaskId node, List<TaskId> path, HashSet<TaskId> done, SortedDictionary<string, IReadOnlyList<TaskId>> found)
    {
        var at = path.IndexOf(node);
        if (at >= 0)
        {
            var cycle = Rotate(path.GetRange(at, path.Count - at));
            _ = found.TryAdd(string.Join(" -> ", cycle.Select(static id => id.Value)), cycle);
            return;
        }

        if (done.Contains(node) || !_deps.TryGetValue(node, out var deps))
        {
            return;
        }

        path.Add(node);
        foreach (var dep in deps)
        {
            Walk(dep, path, done, found);
        }

        path.RemoveAt(path.Count - 1);
        _ = done.Add(node);
    }

    private static List<TaskId> Rotate(List<TaskId> cycle)
    {
        var first = cycle.IndexOf(cycle.MinBy(static id => id.Value, StringComparer.Ordinal));
        var rotated = cycle.Skip(first).Concat(cycle.Take(first)).ToList();
        rotated.Add(rotated[0]);
        return rotated;
    }

    private HashSet<TaskId> Reach(TaskId from)
    {
        if (_reach.TryGetValue(from, out var known))
        {
            return known;
        }

        var seen = new HashSet<TaskId>();
        var stack = new Stack<TaskId>(_deps.GetValueOrDefault(from) ?? []);
        while (stack.TryPop(out var next))
        {
            if (seen.Add(next))
            {
                foreach (var dep in _deps.GetValueOrDefault(next) ?? [])
                {
                    stack.Push(dep);
                }
            }
        }

        _reach[from] = seen;
        return seen;
    }
}
