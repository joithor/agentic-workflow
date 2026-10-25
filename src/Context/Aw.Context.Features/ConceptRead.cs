using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Features;

/// <summary>One read's inputs for <see cref="ConceptBuilder"/>.</summary>
/// <param name="Basis">The map version and repo the answer is for.</param>
/// <param name="Graph">The graph the caller may see.</param>
/// <param name="Text">The caller's text policy.</param>
internal sealed record ConceptRead(ConceptsBasis Basis, GraphSnapshot Graph, MapText Text);
