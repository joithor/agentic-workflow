using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>
/// The rules of <see cref="MapAreas"/> over one graph. A feature's area is the root of its part-of chain (the smallest parent at each step, the smallest key on a cycle); every other node
/// takes the area most of the nodes it points at (or that point at it) are in, one vote per edge, ties to the smallest key, the other areas it reaches in 'also in' (at most five).
/// An area no vote reaches is the unassigned group, and unassigned votes count for nothing.
/// </summary>
internal sealed class AreaAssigner
{
    private const int AlsoInCap = 5;
    private readonly Dictionary<string, NodeKind> _kinds;
    private readonly Dictionary<string, List<GraphEdge>> _out = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphEdge>> _in = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodeArea> _areas = new(StringComparer.Ordinal);

    internal AreaAssigner(GraphSnapshot graph)
    {
        _kinds = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Key.Kind, StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => _kinds.ContainsKey(e.From.Value) && _kinds.ContainsKey(e.To.Value)))
        {
            Add(_out, edge.From.Value, edge);
            Add(_in, edge.To.Value, edge);
        }
    }

    internal AreaIndex Run()
    {
        AssignFeatures();
        Vote(NodeKind.Surface, key => Targets(key, EdgeType.PartOf));
        Vote(NodeKind.Action, key => Targets(key, EdgeType.On));
        Vote(NodeKind.Flow, key => Sources(key, EdgeType.StepOf));
        Vote(NodeKind.Thing, key => Sources(key, EdgeType.Reads, EdgeType.Writes));
        Vote(NodeKind.Code, key => Targets(key, EdgeType.Implements, EdgeType.Tests));
        AssignIntent();
        foreach (var key in _kinds.Keys.Where(k => !_areas.ContainsKey(k)))
        {
            _areas[key] = new NodeArea(AreaIndex.UnassignedKey, []);
        }

        return new AreaIndex(_areas);
    }

    private static void Add(Dictionary<string, List<GraphEdge>> index, string key, GraphEdge edge)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(edge);
    }

    private IEnumerable<string> Targets(string key, params EdgeType[] types) =>
        _out.TryGetValue(key, out var edges) ? edges.Where(e => types.Contains(e.Type)).Select(e => e.To.Value) : [];

    private IEnumerable<string> Sources(string key, params EdgeType[] types) =>
        _in.TryGetValue(key, out var edges) ? edges.Where(e => types.Contains(e.Type)).Select(e => e.From.Value) : [];

    private IEnumerable<string> Keys(NodeKind kind) => _kinds.Where(p => p.Value == kind).Select(p => p.Key).Order(StringComparer.Ordinal);

    private void AssignFeatures()
    {
        var parents = Keys(NodeKind.Feature).ToDictionary(k => k, k => Targets(k, EdgeType.PartOf).Where(t => _kinds[t] == NodeKind.Feature).Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var roots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (feature, above) in parents)
        {
            var root = Root(feature, parents, roots);
            var others = above.Select(p => Root(p, parents, roots)).Where(r => r != root).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(AlsoInCap);
            _areas[feature] = new NodeArea(root, [.. others]);
        }
    }

    // Walks the smallest-parent chain; a chain that meets itself roots at the smallest key on the cycle.
    private static string Root(string start, Dictionary<string, List<string>> parents, Dictionary<string, string> roots)
    {
        var path = new List<string>();
        var onPath = new Dictionary<string, int>(StringComparer.Ordinal);
        var current = start;
        string root;
        while (true)
        {
            if (roots.TryGetValue(current, out var known))
            {
                root = known;
                break;
            }

            if (onPath.TryGetValue(current, out var at))
            {
                root = path.Skip(at).Order(StringComparer.Ordinal).First();
                break;
            }

            onPath[current] = path.Count;
            path.Add(current);
            if (parents[current].Count == 0)
            {
                root = current;
                break;
            }

            current = parents[current][0];
        }

        foreach (var member in path)
        {
            roots[member] = root;
        }

        return root;
    }

    private void Vote(NodeKind kind, Func<string, IEnumerable<string>> reaches)
    {
        foreach (var key in Keys(kind))
        {
            if (Tally(reaches(key)) is { } area)
            {
                _areas[key] = area;
            }
        }
    }

    // Decisions and records lean on each other and on code, so they settle in passes: each pass reads only what earlier passes settled.
    private void AssignIntent()
    {
        var pending = Keys(NodeKind.Decision).Concat(Keys(NodeKind.Record)).ToList();
        while (pending.Count > 0)
        {
            var settled = new List<(string Key, NodeArea Area)>();
            foreach (var key in pending)
            {
                if (Tally(IntentReaches(key)) is { } area)
                {
                    settled.Add((key, area));
                }
            }

            if (settled.Count == 0)
            {
                return;
            }

            foreach (var (key, area) in settled)
            {
                _areas[key] = area;
            }

            pending = [.. pending.Where(k => !_areas.ContainsKey(k))];
        }
    }

    private IEnumerable<string> IntentReaches(string key)
    {
        if (_kinds[key] == NodeKind.Record)
        {
            return Targets(key, EdgeType.Mentions);
        }

        var decided = Targets(key, EdgeType.Decides).ToList();
        return decided.Exists(HasArea) ? decided : Targets(key, EdgeType.Quotes);
    }

    private bool HasArea(string key) => _areas.TryGetValue(key, out var area) && area.Area != AreaIndex.UnassignedKey;

    private NodeArea? Tally(IEnumerable<string> reached)
    {
        var votes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in reached.Where(HasArea))
        {
            var area = _areas[key].Area;
            votes[area] = votes.GetValueOrDefault(area) + 1;
        }

        if (votes.Count == 0)
        {
            return null;
        }

        var ranked = votes.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key).ToList();
        return new NodeArea(ranked[0], [.. ranked.Skip(1).Take(AlsoInCap)]);
    }
}
