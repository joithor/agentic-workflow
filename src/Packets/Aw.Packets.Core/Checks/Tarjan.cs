using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Checks;

/// <summary>Tarjan's strongly connected components over the task graph; dependencies on unknown tasks are skipped.</summary>
/// <param name="deps">Each task's dependencies.</param>
internal sealed class Tarjan(IReadOnlyDictionary<TaskId, IReadOnlyList<TaskId>> deps)
{
    private readonly Dictionary<TaskId, int> _index = [];
    private readonly Dictionary<TaskId, int> _low = [];
    private readonly Stack<TaskId> _stack = new();
    private readonly HashSet<TaskId> _onStack = [];

    /// <summary>The groups found so far.</summary>
    internal List<List<TaskId>> Groups { get; } = [];

    /// <summary>Visits <paramref name="node"/> unless it was visited already.</summary>
    /// <param name="node">A task.</param>
    internal void Visit(TaskId node)
    {
        if (_index.ContainsKey(node))
        {
            return;
        }

        _index[node] = _low[node] = _index.Count;
        _stack.Push(node);
        _ = _onStack.Add(node);
        foreach (var dep in deps[node].Where(deps.ContainsKey))
        {
            if (!_index.TryGetValue(dep, out var index))
            {
                Visit(dep);
                _low[node] = Math.Min(_low[node], _low[dep]);
            }
            else if (_onStack.Contains(dep))
            {
                _low[node] = Math.Min(_low[node], index);
            }
        }

        if (_low[node] == _index[node])
        {
            Groups.Add(Pop(node));
        }
    }

    private List<TaskId> Pop(TaskId root)
    {
        var group = new List<TaskId>();
        TaskId member;
        do
        {
            member = _stack.Pop();
            _ = _onStack.Remove(member);
            group.Add(member);
        }
        while (member != root);
        return group;
    }
}
