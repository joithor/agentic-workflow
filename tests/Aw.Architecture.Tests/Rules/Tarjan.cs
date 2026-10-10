namespace Aw.Architecture.Tests.Rules;

/// <summary>Tarjan's strongly connected components over a namespace graph.</summary>
/// <param name="graph">Adjacency sets by node.</param>
internal sealed class Tarjan(Dictionary<string, HashSet<string>> graph)
{
    private readonly Dictionary<string, int> _low = new(StringComparer.Ordinal);
    private readonly Stack<string> _stack = new();
    private readonly HashSet<string> _onStack = new(StringComparer.Ordinal);

    /// <summary>The visit order of each node.</summary>
    public Dictionary<string, int> Index { get; } = new(StringComparer.Ordinal);

    /// <summary>The components found so far, each sorted.</summary>
    public List<IReadOnlyList<string>> Components { get; } = [];

    /// <summary>Visits a node and everything reachable from it.</summary>
    /// <param name="node">The node.</param>
    public void Visit(string node)
    {
        Index[node] = _low[node] = Index.Count;
        _stack.Push(node);
        _ = _onStack.Add(node);
        foreach (var next in graph[node].Order(StringComparer.Ordinal))
        {
            if (!Index.ContainsKey(next))
            {
                Visit(next);
                _low[node] = Math.Min(_low[node], _low[next]);
            }
            else if (_onStack.Contains(next) && Index.TryGetValue(next, out var order))
            {
                _low[node] = Math.Min(_low[node], order);
            }
        }

        if (_low[node] == Index[node])
        {
            Components.Add(Pop(node));
        }
    }

    private List<string> Pop(string root)
    {
        var component = new List<string>();
        string top;
        do
        {
            top = _stack.Pop();
            _ = _onStack.Remove(top);
            component.Add(top);
        }
        while (top != root);
        component.Sort(StringComparer.Ordinal);
        return component;
    }
}
