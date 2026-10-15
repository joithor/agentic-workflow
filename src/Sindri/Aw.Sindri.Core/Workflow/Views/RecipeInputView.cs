using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe input as the recipe view serves it (E-22).</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">The fact type the input is declared with; the instance's fact row of the same key serves the same type.</param>
/// <param name="Kind">The subject adapter, read from the first input only.</param>
/// <param name="Required">Whether required.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInputView(
    string Key,
    string? Label,
    FactType Type,
    string? Kind,
    bool Required);
