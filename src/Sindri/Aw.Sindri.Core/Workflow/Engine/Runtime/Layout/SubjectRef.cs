namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>What an instance is about, from the recipe's first input (E-20).</summary>
/// <param name="Kind">The input's kind.</param>
/// <param name="Key">The subject key.</param>
/// <param name="Label">The label the app shows.</param>
/// <param name="Href">A link to the subject, when it has one.</param>
public sealed record SubjectRef(
    string Kind,
    string Key,
    string Label,
    string? Href);
