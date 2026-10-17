using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Kernel;

namespace Aw.Context.Core.Code;

/// <summary>Builds the graph half of the code lens (S-CL): pure over an applied graph, no repo read.</summary>
public static class CodeLensBuilder
{
    /// <summary>Builds the view without lexical data.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchors">The anchors (keys, aliases or labels).</param>
    /// <returns>The view; SND-CONTEXT-003 when none resolves, SND-CONTEXT-037 for an ambiguous name.</returns>
    public static Result<CodeView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> anchors)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(anchors);
        var resolved = Resolve(graph, [.. anchors.Distinct(StringComparer.Ordinal)]);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (scopes, missing) = resolved.Value;
        var reach = new CodeReach(graph, scopes);
        var files = CodeFiles.Of(reach);
        var tests = CodeTests.Of(reach);
        var things = CodeThings.Of(reach);
        var (linkedFiles, linkedTests) = CodeFiles.Link(files, tests);
        var listedFiles = linkedFiles.Take(CodeRules.MaxListed).ToList();
        var listedTests = linkedTests.Take(CodeRules.MaxListed).ToList();
        var listedThings = things.Take(CodeRules.MaxListed).ToList();
        return new CodeView(
            1,
            [.. scopes.Select(Anchor)],
            missing,
            basis.Map,
            basis.Draft,
            basis.AsOf,
            listedFiles,
            listedTests,
            new CodeData(listedThings, []),
            CodeSymbols.Unavailable,
            CodeSymbols.Unavailable,
            new CodeLensCounts(linkedFiles.Count, linkedTests.Count, things.Count, SchemaFiles: 0, linkedFiles.Count(f => f.TestedBy.Count == 0), Unlocated: 0),
            new CodeLensTruncated(linkedFiles.Count > CodeRules.MaxListed, linkedTests.Count > CodeRules.MaxListed, things.Count > CodeRules.MaxListed, SchemaFiles: false, SearchedFiles: false),
            [],
            CodeEdges.Of(graph, reach.Union, listedFiles, listedTests, listedThings));
    }

    /// <summary>The search terms of a data thing: the forms of its key name, its label and every alias that targets it.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="thing">The thing's key.</param>
    /// <returns>The forms; empty when the thing is not searchable.</returns>
    public static IReadOnlyList<string> ThingTerms(GraphSnapshot graph, NodeKey thing)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var names = new List<string> { CodeTerms.KeyName(thing.Value) };
        if (graph.Nodes.FirstOrDefault(n => n.Key.Value == thing.Value) is { } node)
        {
            names.Add(node.Label);
        }

        names.AddRange(graph.Aliases.Where(a => a.Target.Value == thing.Value).Select(a => CodeTerms.KeyName(a.Alias.Value)));
        return CodeTerms.Forms(names);
    }

    // The anchors that resolve (de-duplicated by node), the values that don't (as given). An ambiguous name refuses the call; with nothing resolved the first miss is the answer.
    private static Result<(List<LensScope> Scopes, List<string> Missing)> Resolve(GraphSnapshot graph, List<string> given)
    {
        var scopes = new List<LensScope>();
        var missing = new List<string>();
        Error? first = null;
        foreach (var raw in given)
        {
            var node = AnchorMatch.Resolve(graph, raw);
            if (node.IsSuccess)
            {
                if (!scopes.Exists(s => s.Anchor.Key.Value == node.Value.Key.Value) && LensScope.Of(graph, node.Value.Key, depth: 0) is { } scope)
                {
                    scopes.Add(scope);
                }

                continue;
            }

            if (node.Error.Code == ContextErrors.AmbiguousAnchor.Code)
            {
                return node.Error;
            }

            missing.Add(raw);
            first ??= node.Error;
        }

        return scopes.Count == 0 && first is { } none ? none : (scopes, missing);
    }

    private static LensAnchor Anchor(LensScope scope) =>
        new(scope.Anchor.Key, scope.Anchor.Label, scope.Anchor.Key.Kind, TrustPolicy.ForNode(scope.Anchor, LensScope.Confirmed(scope.Anchor.Provenance)));
}
