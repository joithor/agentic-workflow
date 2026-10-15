using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe input as the recipe view serves it (E-22).</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">How it is asked.</param>
/// <param name="Kind">The subject adapter, read from the first input only.</param>
/// <param name="Required">Whether required.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeInputView(
    string Key,
    string? Label,
    InputType Type,
    string? Kind,
    bool Required);
