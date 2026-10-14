using Aw.Kernel;

namespace Aw.Context.Core.Graph;

/// <summary>Resolves what a caller names to graph nodes (spec §8.1): a key, an alias or a name for the 'node' shape; whole names in a question for the 'text' shape.</summary>
public static class AnchorMatch
{
    /// <summary>The node <paramref name="name"/> names: a key, then an alias, then a label (case ignored).</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="name">A node key, alias or label.</param>
    /// <returns>The node; SND-CONTEXT-037 when a label names several (the message lists them); SND-CONTEXT-003 with suggestions when nothing matches.</returns>
    public static Result<GraphNode> Resolve(GraphSnapshot graph, string name) => throw new NotSupportedException();

    /// <summary>The nodes a question names by their whole label, and others it comes close to.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="question">The question.</param>
    /// <returns>The anchors and suggestions.</returns>
    public static TextAnchors FromText(GraphSnapshot graph, string question) => throw new NotSupportedException();
}
