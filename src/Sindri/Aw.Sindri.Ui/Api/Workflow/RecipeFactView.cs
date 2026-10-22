using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A fact a recipe state reads or writes.</summary>
/// <param name="Key">The fact key as the fact rows serve it ('fact:scope').</param>
/// <param name="Label">The recipe's label for it; read as optional.</param>
internal sealed record RecipeFactView(
    string Key,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label = null);
