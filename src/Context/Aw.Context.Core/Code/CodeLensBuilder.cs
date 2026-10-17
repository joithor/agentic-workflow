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
    public static Result<CodeView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> anchors) => throw new NotSupportedException();

    /// <summary>The search terms of a data thing: the forms of its key name, its label and every alias that targets it.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="thing">The thing's key.</param>
    /// <returns>The forms; empty when the thing is not searchable.</returns>
    public static IReadOnlyList<string> ThingTerms(GraphSnapshot graph, NodeKey thing) => throw new NotSupportedException();
}
