using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe output declaration (E-22).</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">The type.</param>
/// <param name="From">The state that produces it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeOutputView(
    string Key,
    string? Label,
    FactType Type,
    string From);
