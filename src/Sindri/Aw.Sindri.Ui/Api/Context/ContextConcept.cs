namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One core concept of the product (the concept band, proposed contract).</summary>
/// <param name="Id">The concept's id; the value of the map read's concept parameter.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">One short summary.</param>
/// <param name="Size">What it holds.</param>
/// <param name="Source">model (the model proposed it) or override (a person set it).</param>
internal sealed record ContextConcept(string Id, string Name, string Summary, ConceptSize Size, string Source = "model");
