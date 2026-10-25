using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>Builds the glossary answer from a graph: the terms stored on its concept nodes. Pure apart from the text policy.</summary>
public static class ConceptGlossaryBuilder
{
    /// <summary>The terms of every concept of a graph.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view.</returns>
    public static ConceptGlossaryView Build(ConceptsBasis basis, GraphSnapshot graph, MapText text) => throw new NotSupportedException();
}
