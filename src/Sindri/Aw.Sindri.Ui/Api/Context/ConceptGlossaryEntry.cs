namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One term of the product's glossary (provisional, additive): every field but the term is optional.</summary>
/// <param name="Term">The term.</param>
/// <param name="Definition">What it means; null when none is served.</param>
/// <param name="Concepts">The ids of the concepts it belongs to; null when none is served.</param>
internal sealed record ConceptGlossaryEntry(string Term, string? Definition = null, IReadOnlyList<string>? Concepts = null);
