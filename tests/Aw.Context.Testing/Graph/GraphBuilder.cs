using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Graph;

/// <summary>Builds small graphs for unit tests: nodes by key (their label the key's name unless given), edges by key and type.</summary>
public sealed class GraphBuilder
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];
    private readonly List<NodeAlias> _aliases = [];

    /// <summary>Adds a node.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="label">Its label, or null for the key's name.</param>
    /// <param name="provenance">Its provenance.</param>
    /// <param name="confidence">Its confidence.</param>
    /// <returns>This builder.</returns>
    public GraphBuilder Node(string key, string? label = null, Provenance provenance = Provenance.Human, double confidence = 1.0)
    {
        var k = NodeKey.From(key);
        _nodes.Add(new GraphNode(k, label ?? k.Name, provenance, confidence));
        return this;
    }

    /// <summary>Adds an edge (its ends are added as nodes when missing).</summary>
    /// <param name="from">The source key.</param>
    /// <param name="type">The type.</param>
    /// <param name="to">The target key.</param>
    /// <param name="order">The flow order, for step-of.</param>
    /// <param name="provenance">Its provenance.</param>
    /// <param name="confidence">Its confidence.</param>
    /// <param name="evidence">The key of the one record it rests on (such as 'repo:aw-split-panes/src/a.ts'), or null.</param>
    /// <returns>This builder.</returns>
    public GraphBuilder Edge(string from, EdgeType type, string to, int? order = null, Provenance provenance = Provenance.Human, double confidence = 1.0, string? evidence = null)
    {
        foreach (var key in new[] { from, to }.Where(k => !_nodes.Exists(n => n.Key.Value == k)))
        {
            _ = Node(key);
        }

        _edges.Add(new GraphEdge(NodeKey.From(from), NodeKey.From(to), type, provenance, confidence, Order: order, Evidence: evidence is null ? null : [new EvidenceRef(RecordKey.From(evidence))]));
        return this;
    }

    /// <summary>Adds an alias.</summary>
    /// <param name="alias">The other key.</param>
    /// <param name="target">The node's key.</param>
    /// <returns>This builder.</returns>
    public GraphBuilder Alias(string alias, string target)
    {
        _aliases.Add(new NodeAlias(NodeKey.From(alias), NodeKey.From(target)));
        return this;
    }

    /// <summary>The graph.</summary>
    /// <returns>It.</returns>
    public GraphSnapshot Build() => new([.. _nodes], [.. _edges], [.. _aliases], []);
}
