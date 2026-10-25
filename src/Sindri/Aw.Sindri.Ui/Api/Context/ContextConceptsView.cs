namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// GET /sindri/context/concepts (the concept band, proposed contract): the product's core concepts, the first layer of the context map.
/// </summary>
/// <param name="ApiVersion">The answer's version.</param>
/// <param name="Repo">The repo the concepts are of.</param>
/// <param name="State">ok, empty (no concepts yet), building or unavailable.</param>
/// <param name="Concepts">The concepts; absent unless the state is ok.</param>
/// <param name="Other">The concept of the unassigned nodes.</param>
/// <param name="Links">The links between concepts; absent when the server does not serve them.</param>
/// <param name="Code">The error code when the state is unavailable.</param>
internal sealed record ContextConceptsView(
    int ApiVersion,
    string Repo,
    string State,
    IReadOnlyList<ContextConcept>? Concepts = null,
    ConceptOther? Other = null,
    IReadOnlyList<ConceptLink>? Links = null,
    string? Code = null);
