using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// Tarjan's strongly connected components over the task graph, iterative (an explicit stack of frames), so a long
/// dependency chain can't overflow the call stack. Dependencies on unknown tasks are skipped.
/// </summary>
/// <param name="deps">Each task's dependencies.</param>
internal sealed class Tarjan(IReadOnlyDictionary<TaskId, IReadOnlyList<TaskId>> deps)
{
    private readonly Dictionary<TaskId, int> _index = [];
    private readonly Dictionary<TaskId, int> _low = [];
    private readonly Stack<TaskId> _stack = new();
    private readonly HashSet<TaskId> _onStack = [];

    /// <summary>The groups found so far.</summary>
    internal List<List<TaskId>> Groups { get; } = [];

    /// <summary>Visits <paramref name="start"/> and everything it reaches, unless it was visited already.</summary>
    /// <param name="start">A task.</param>
    internal void Visit(TaskId start)
    {
        if (_index.ContainsKey(start))
        {
            return;
        }

        var frames = new Stack<(TaskId Node, int Next)>();
        Open(start, frames);
        while (frames.TryPop(out var frame))
        {
            var (node, next) = frame;
            var list = deps[node];
            if (next < list.Count)
            {
                frames.Push((node, next + 1));
                Step(node, list[next], frames);
                continue;
            }

            if (_low[node] == _index[node])
            {
                Groups.Add(Pop(node));
            }

            if (frames.TryPeek(out var parent))
            {
                _low[parent.Node] = Math.Min(_low[parent.Node], _low[node]);
            }
        }
    }

    private void Step(TaskId node, TaskId dep, Stack<(TaskId Node, int Next)> frames)
    {
        if (!deps.ContainsKey(dep))
        {
            return;
        }

        if (!_index.TryGetValue(dep, out var index))
        {
            Open(dep, frames);
        }
        else if (_onStack.Contains(dep))
        {
            _low[node] = Math.Min(_low[node], index);
        }
    }

    private void Open(TaskId node, Stack<(TaskId Node, int Next)> frames)
    {
        _index[node] = _low[node] = _index.Count;
        _stack.Push(node);
        _ = _onStack.Add(node);
        frames.Push((node, 0));
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
