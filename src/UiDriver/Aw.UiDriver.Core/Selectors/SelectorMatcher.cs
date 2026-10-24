using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Core.Selectors;

/// <summary>How a selector picks elements from a tree walked in order.</summary>
internal static class SelectorMatcher
{
    /// <summary>True when <paramref name="node"/> satisfies every field the selector gives (the index is not a field of one node).</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="node">The element's facts.</param>
    /// <returns>Whether it matches.</returns>
    internal static bool Matches(Selector selector, NodeFacts node) =>
        (selector.NodeId is null || string.Equals(selector.NodeId, node.Id, StringComparison.Ordinal))
        && (selector.AutomationId is null || string.Equals(selector.AutomationId, node.AutomationId, StringComparison.Ordinal))
        && (selector.Name is null || string.Equals(selector.Name, node.Name, StringComparison.Ordinal))
        && (selector.Type is null || string.Equals(selector.Type, node.Type, StringComparison.Ordinal))
        && (selector.Text is null || string.Equals(selector.Text, node.Text, StringComparison.Ordinal))
        && (selector.TextContains is null || (node.Text?.Contains(selector.TextContains, StringComparison.Ordinal) ?? false));

    /// <summary>The nodes a selector matches, in the order given.</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="nodes">The tree's nodes in tree order.</param>
    /// <returns>All matches; the index does not narrow this list.</returns>
    internal static IReadOnlyList<NodeFacts> All(Selector selector, IEnumerable<NodeFacts> nodes) => [.. nodes.Where(n => Matches(selector, n))];

    /// <summary>The one node a verb acts on: the match at the selector's index (the first by default).</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="nodes">The tree's nodes in tree order.</param>
    /// <returns>The node, or null when there are fewer matches than the index asks for.</returns>
    internal static NodeFacts? Pick(Selector selector, IEnumerable<NodeFacts> nodes)
    {
        var matches = All(selector, nodes);
        var index = selector.Index ?? 0;
        return index >= 0 && index < matches.Count ? matches[index] : null;
    }
}
