using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe input as the recipe view serves it (E-22).</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">The fact type the input is declared with; the instance's fact row of the same key serves the same type.</param>
/// <param name="Kind">The subject adapter, served for every input (never null).</param>
/// <param name="Required">Whether required: false for an input with a generate template.</param>
/// <param name="Generate">The generate template as written, or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInputView(
    string Key,
    string? Label,
    FactType Type,
    string Kind,
    bool Required,
    string? Generate);
