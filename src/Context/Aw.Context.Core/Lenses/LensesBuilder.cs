using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the lenses view around one node (spec §8.9, S-4). Pure over an applied graph.</summary>
public static class LensesBuilder
{
    /// <summary>The cap on each list of a band.</summary>
    public const int BandCap = 100;

    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchor">The anchor (a key or an alias).</param>
    /// <param name="depth">How far the neighbors reach and the cascade runs, 0 to 4.</param>
    /// <param name="checkedPairs">The model-checked decision pairs; a 'contradicts' pair shows as a conflict (intent against intent).</param>
    /// <returns>The view, or SND-CONTEXT-003 when the graph has no such node.</returns>
    public static Result<LensesView> Build(LensBasis basis, GraphSnapshot graph, NodeKey anchor, int depth, IReadOnlyList<Linking.DecisionConflict>? checkedPairs = null)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(graph);
        var clamped = Math.Clamp(depth, 0, GraphWalk.MaxDepth);
        if (LensScope.Of(graph, anchor, clamped) is not { } scope)
        {
            return ContextErrors.UnknownAnchor.With($"no node '{(anchor.Value.Length <= 80 ? anchor.Value : "(a long key)")}' in this map (fix: sindri context map status)");
        }

        var intent = LensIntent.Build(scope);
        var (product, productKeys) = LensProduct.Build(scope, clamped);
        var (code, codeKeys) = LensCode.Build(scope);
        var items = productKeys.Concat(codeKeys).Concat(scope.IntoScope(EdgeType.Mentions, EdgeType.Decides).Select(e => e.From.Value)).ToHashSet(StringComparer.Ordinal);
        var anchorTrust = TrustPolicy.ForNode(scope.Anchor, LensScope.Confirmed(scope.Anchor.Provenance));
        return new LensesView(1, new LensAnchor(scope.Anchor.Key, scope.Anchor.Label, scope.Anchor.Key.Kind, anchorTrust), basis.Map, basis.Draft, clamped, basis.AsOf,
            new LensBands(intent, product, code), LensEdges.Across(graph, items), LensConflicts.Of(scope, checkedPairs ?? []), LensCoverageBuilder.Of(scope), LensSearchedBuilder.Placeholder, [], new Dictionary<string, Results.Citation>(StringComparer.Ordinal));
    }

    /// <summary>Builds the schema 2 view over several anchors (scoping spec C1): the union of their bands.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="anchors">The anchors as given (keys, aliases or labels); duplicates count once.</param>
    /// <param name="depth">How far the neighbors reach and the cascade runs, 0 to 4.</param>
    /// <param name="checkedPairs">The model-checked decision pairs.</param>
    /// <returns>The view; SND-CONTEXT-006 for no anchor or more than 20; SND-CONTEXT-037 for an ambiguous name; SND-CONTEXT-003 when none resolves.</returns>
    public static Result<LensesSetView> BuildSet(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> anchors, int depth, IReadOnlyList<Linking.DecisionConflict>? checkedPairs = null) =>
        throw new NotSupportedException($"red {MapCoverageBuilder.Of(graph, new HashSet<string>(StringComparer.Ordinal), basis)}");
}
