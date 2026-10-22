namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the DocumentHits operation.</summary>
/// <param name="Documents">The Documents Linear serves.</param>
internal sealed record LinearDocumentsData(LinearConnection<LinearDocument> Documents);
