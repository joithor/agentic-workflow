using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Conflicts on the own scope (spec §8.9): a decision that supersedes another, once per decided node. 'contradicts' comes with S-CF.</summary>
internal static class LensConflicts
{
    private const int ShortKey = 17;

    /// <summary>The conflicts of the scope, ordered by node, decision and the one it supersedes.</summary>
    internal static List<LensConflict> Of(LensScope scope)
    {
        var supersedes = scope.Graph.Edges.Where(e => e.Type == EdgeType.Supersedes).ToList();
        return [.. scope.IntoScope(EdgeType.Decides)
            .SelectMany(decides => supersedes.Where(s => s.From.Value == decides.From.Value).Select(s => (Node: decides.To, Decision: s.From, Other: s.To)))
            .Distinct().OrderBy(c => c.Node.Value, StringComparer.Ordinal).ThenBy(c => c.Decision.Value, StringComparer.Ordinal).ThenBy(c => c.Other.Value, StringComparer.Ordinal)
            .Select(c => new LensConflict(ConflictKind.Supersedes, c.Node, c.Decision, c.Other.Value, $"{Short(c.Decision)} supersedes {Short(c.Other)}", [], []))];
    }

    private static string Short(NodeKey key) => key.Value.Length <= ShortKey ? key.Value : key.Value[..ShortKey];
}
