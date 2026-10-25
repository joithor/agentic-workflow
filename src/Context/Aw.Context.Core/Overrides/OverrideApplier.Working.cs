using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Overrides;

public static partial class OverrideApplier
{
    /// <summary>
    /// One copy of the graph for the whole fold, indexed by node key, alias, link id and edge end, so an accept, a reject, a rename,
    /// a delete, a proposal or a link decision changes a slot in place instead of copying every node and edge (which made one build
    /// O(overrides × graph): minutes on a 24k-node draft). A removed entry leaves a hole, so the graph comes back in the same order.
    /// An add, a move or a merge (rare, a person's edits) runs the copying step on a snapshot and indexes the result again.
    /// </summary>
    private sealed class Working
    {
        private readonly Action? _copied;
        private GraphSnapshot _source = GraphSnapshot.Empty;
        private List<GraphNode?> _nodes = [];
        private List<GraphEdge?> _edges = [];
        private List<NodeAlias?> _aliases = [];
        private Dictionary<string, List<int>> _nodesByKey = [];
        private Dictionary<string, List<int>>? _edgesByLink;   // built on the first link decision: a link id is a hash of the edge
        private Dictionary<string, List<int>> _edgesByEnd = [];
        private Dictionary<string, List<int>> _aliasesByAlias = [];
        private Dictionary<string, List<int>> _aliasesByTarget = [];

        internal Working(GraphSnapshot graph, Action? copied)
        {
            _copied = copied;
            Load(graph);
        }

        /// <summary>The graph as it stands, as a new snapshot (one copy).</summary>
        internal GraphSnapshot Snapshot()
        {
            _copied?.Invoke();
            return _source with { Nodes = [.. _nodes.OfType<GraphNode>()], Edges = [.. _edges.OfType<GraphEdge>()], Aliases = [.. _aliases.OfType<NodeAlias>()] };
        }

        /// <summary>Applies one override; false when it doesn't apply (its target is gone, or the action has nothing to do here).</summary>
        internal bool Apply(MapOverride o)
        {
            if (o.Action is OverrideAction.Add)
            {
                return Add(o);
            }

            if (o.Action is OverrideAction.Propose)
            {
                return Propose(o);
            }

            if (o.Link is { } link)
            {
                return DecideLink(o, link);
            }

            if (Resolve(o.Target) is not { } target)
            {
                return false;
            }

            return o.Action switch
            {
                OverrideAction.Accept => Accept(target),
                OverrideAction.Rename when !string.IsNullOrWhiteSpace(o.Label) => Replace(target, target with { Label = o.Label }),
                OverrideAction.Reject => !o.Rejects(RejectionIdentity.For(target)) || Remove(target.Key.Value),
                OverrideAction.Delete => Remove(target.Key.Value),
                OverrideAction.Move or OverrideAction.Merge => Fits(target, o) && Copying(WithOther(Snapshot(), target, o)),
                OverrideAction.Rename or OverrideAction.Add or OverrideAction.Propose or OverrideAction.Undo => false,
                _ => false,
            };
        }

        /// <summary>Whether an override that didn't apply is orphaned: a link decision whose edge is gone, a node decision whose target no longer resolves.</summary>
        internal bool IsOrphan(MapOverride o) => o.Action switch
        {
            OverrideAction.Add or OverrideAction.Propose or OverrideAction.Undo => false,
            OverrideAction.Reject or OverrideAction.Delete => false,   // satisfied once its target is gone; the row stays, so the item stays rejected if it returns
            _ when o.Link is { } link => FirstEdge(link.Value) is null,
            OverrideAction.Accept or OverrideAction.Rename or OverrideAction.Merge or OverrideAction.Move => Resolve(o.Target) is null,
            _ => false,
        };

        // As GraphSnapshot.Resolve: an alias names its target, and the first node with the key wins.
        private GraphNode? Resolve(NodeKey key)
        {
            var target = Live(_aliasesByAlias, key.Value, _aliases).FirstOrDefault()?.Target.Value ?? key.Value;
            return Live(_nodesByKey, target, _nodes).FirstOrDefault();
        }

        private GraphEdge? FirstEdge(string link) => Live(EdgesByLink(), link, _edges).FirstOrDefault();

        // A proposal's node is added unless its key resolves already; its parent edge comes with it when the parent resolves and fits.
        private bool Propose(MapOverride o)
        {
            if (Resolve(o.Target) is not null)
            {
                return true;
            }

            if (ProposalRows.NodeOf(o) is not { } node)
            {
                return false;
            }

            var parent = o.Proposal?.Parent is { } key ? Resolve(key)?.Key : null;
            AddNode(node);
            if (parent is { } p && EdgeRules.Allows(EdgeType.PartOf, o.Target.Kind, p.Kind))
            {
                AddEdge(new GraphEdge(o.Target, p, EdgeType.PartOf, Provenance.Model, node.Confidence, node.Evidence, At: o.At));
            }

            return true;
        }

        // A person's added node: nothing to do when its key resolves already; refused when its kind can't be added or its named parent is
        // missing or doesn't fit; else the node, and its part-of edge when it names a parent.
        private bool Add(MapOverride o)
        {
            if (Resolve(o.Target) is not null)
            {
                return true;
            }

            var parent = o.Other is { } key ? Resolve(key)?.Key : null;
            if (!Addable(o.Target) || (o.Other is not null && (parent is not { } p || !EdgeRules.Allows(EdgeType.PartOf, o.Target.Kind, p.Kind))))
            {
                return false;
            }

            AddNode(new GraphNode(o.Target, o.Label ?? o.Target.Name, Provenance.Human, At: o.At));
            if (parent is { } under)
            {
                AddEdge(new GraphEdge(o.Target, under, EdgeType.PartOf, At: o.At));
            }

            return true;
        }

        // Whether a move or a merge will change the graph (WithOther's checks), so one that won't is refused without a copy.
        private bool Fits(GraphNode target, MapOverride o)
        {
            var other = o.Other is { } key ? Resolve(key) : null;
            var allowed = o.Action == OverrideAction.Move
                ? EdgeRules.Allows(EdgeType.PartOf, target.Key.Kind, other?.Key.Kind ?? NodeKind.Feature)
                : other?.Key.Kind == target.Key.Kind;
            return other is not null && other.Key.Value != target.Key.Value && allowed;
        }

        // A proposal's own parent edge is confirmed with it.
        private bool Accept(GraphNode target)
        {
            _ = Replace(target, target with { Provenance = Provenance.Human });
            foreach (var i in Slots(_edgesByEnd, target.Key.Value))
            {
                if (_edges[i] is { Type: EdgeType.PartOf, Provenance: Provenance.Model } edge && edge.From.Value == target.Key.Value)
                {
                    _edges[i] = edge with { Provenance = Provenance.Human };
                }
            }

            return true;
        }

        private bool DecideLink(MapOverride o, LinkId link)
        {
            if (FirstEdge(link.Value) is not { } edge || o.Action is not (OverrideAction.Accept or OverrideAction.Reject))
            {
                return false;
            }

            foreach (var i in Slots(EdgesByLink(), link.Value).Where(i => ReferenceEquals(_edges[i], edge)))
            {
                _edges[i] = o.Action == OverrideAction.Accept ? edge with { Provenance = Provenance.Human } : null;
            }

            return true;
        }

        private bool Replace(GraphNode old, GraphNode next)
        {
            foreach (var i in Slots(_nodesByKey, old.Key.Value).Where(i => ReferenceEquals(_nodes[i], old)))
            {
                _nodes[i] = next;
            }

            return true;
        }

        // Every node with the key, every edge at either end of it, every alias that names it.
        private bool Remove(string key)
        {
            Clear(_nodes, Slots(_nodesByKey, key));
            Clear(_edges, Slots(_edgesByEnd, key));
            Clear(_aliases, Slots(_aliasesByTarget, key));
            return true;
        }

        // The result of a copying step on a snapshot (null: it didn't apply), indexed again.
        private bool Copying(GraphSnapshot? next)
        {
            if (next is not null)
            {
                Load(next);
            }

            return next is not null;
        }

        private void Load(GraphSnapshot graph)
        {
            (_source, _nodes, _edges, _aliases) = (graph, [], [], []);
            (_nodesByKey, _edgesByLink, _edgesByEnd) = (new(StringComparer.Ordinal), null, new(StringComparer.Ordinal));
            (_aliasesByAlias, _aliasesByTarget) = (new(StringComparer.Ordinal), new(StringComparer.Ordinal));
            foreach (var node in graph.Nodes)
            {
                AddNode(node);
            }

            foreach (var edge in graph.Edges)
            {
                AddEdge(edge);
            }

            foreach (var alias in graph.Aliases)
            {
                _aliases.Add(alias);
                At(_aliasesByAlias, alias.Alias.Value).Add(_aliases.Count - 1);
                At(_aliasesByTarget, alias.Target.Value).Add(_aliases.Count - 1);
            }
        }

        private void AddNode(GraphNode node)
        {
            _nodes.Add(node);
            At(_nodesByKey, node.Key.Value).Add(_nodes.Count - 1);
        }

        private void AddEdge(GraphEdge edge)
        {
            _edges.Add(edge);
            var slot = _edges.Count - 1;
            if (_edgesByLink is not null)
            {
                At(_edgesByLink, LinkIds.For(edge).Value).Add(slot);
            }

            At(_edgesByEnd, edge.From.Value).Add(slot);
            if (edge.To.Value != edge.From.Value)
            {
                At(_edgesByEnd, edge.To.Value).Add(slot);
            }
        }

        private Dictionary<string, List<int>> EdgesByLink()
        {
            if (_edgesByLink is null)
            {
                _edgesByLink = new(StringComparer.Ordinal);
                for (var i = 0; i < _edges.Count; i++)
                {
                    if (_edges[i] is { } edge)
                    {
                        At(_edgesByLink, LinkIds.For(edge).Value).Add(i);
                    }
                }
            }

            return _edgesByLink;
        }

        private static List<int> At(Dictionary<string, List<int>> index, string key)
        {
            if (!index.TryGetValue(key, out var slots))
            {
                slots = [];
                index[key] = slots;
            }

            return slots;
        }

        private static List<int> Slots(Dictionary<string, List<int>> index, string key) => index.GetValueOrDefault(key) ?? [];

        // The entries still in place under a key, in graph order.
        private static IEnumerable<T> Live<T>(Dictionary<string, List<int>> index, string key, List<T?> items)
            where T : class => Slots(index, key).Select(i => items[i]).OfType<T>();

        private static void Clear<T>(List<T?> items, List<int> slots)
            where T : class
        {
            foreach (var i in slots)
            {
                items[i] = null;
            }
        }
    }
}
