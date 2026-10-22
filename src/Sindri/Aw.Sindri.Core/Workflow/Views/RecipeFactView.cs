using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A fact a recipe state reads or writes.</summary>
/// <param name="Key">The fact key as the instance's fact rows serve it ('fact:scope').</param>
/// <param name="Label">The recipe's label for the fact, or null (left out of the JSON) when it declares none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeFactView(
    string Key,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label = null);
