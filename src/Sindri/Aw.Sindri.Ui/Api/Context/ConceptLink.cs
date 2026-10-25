namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A link between two concepts, by id (the concept band, proposed addition to the contract: the filter keeps a match's links).</summary>
/// <param name="From">One end's id.</param>
/// <param name="To">The other end's id.</param>
internal sealed record ConceptLink(string From, string To);
