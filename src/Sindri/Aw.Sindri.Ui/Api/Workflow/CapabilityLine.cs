namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One capability in plain words, with its machine id.</summary>
/// <param name="Id">The capability id.</param>
/// <param name="Sentence">What it lets the recipe do.</param>
internal sealed record CapabilityLine(string Id, string Sentence);
