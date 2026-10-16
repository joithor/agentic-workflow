using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One recipe input and whether the instance has it (E-20).</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The input's declared kind (the subject adapter), exactly as the recipe view serves it (never null).</param>
/// <param name="Required">Whether it is required: false for an input with a generate template.</param>
/// <param name="Present">Whether the instance holds it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InputPresence(
    string Key,
    string Label,
    string Kind,
    bool Required,
    bool Present);
