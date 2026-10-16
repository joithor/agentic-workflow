using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>What an instance is about (screens spec section 5.4).</summary>
/// <param name="Kind">The first input's subject kind (always equal to inputs[0].kind; never null, RCP-INPUT-001).</param>
/// <param name="Key">The subject key.</param>
/// <param name="Label">The label.</param>
/// <param name="Href">A link to it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubjectRef(
    string Kind,
    string Key,
    string Label,
    string? Href);
