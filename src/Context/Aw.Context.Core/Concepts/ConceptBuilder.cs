using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>Builds the concepts answers from a graph (the read side of the concept layer). Pure apart from the text policy it writes names with.</summary>
public static class ConceptBuilder
{
    /// <summary>One page of the concepts of a graph.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="query">The filter and the page.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, or SND-CONTEXT-006 for a bad page.</returns>
    public static Result<ConceptsView> Build(ConceptsBasis basis, ConceptsQuery query, GraphSnapshot graph, MapText text) => throw new NotSupportedException();

    /// <summary>One page of one concept's members.</summary>
    /// <param name="basis">The map version and repo the answer is for.</param>
    /// <param name="query">The concept and the page.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, SND-CONTEXT-006 for a bad page, or SND-CONTEXT-003 for a concept the map lacks.</returns>
    public static Result<ConceptMembersView> Members(ConceptsBasis basis, ConceptMembersQuery query, GraphSnapshot graph, MapText text) => throw new NotSupportedException();
}
