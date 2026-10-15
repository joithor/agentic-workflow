using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe as an instance pins it (E-20).</summary>
/// <param name="Name">The recipe id.</param>
/// <param name="Hash">The pinned hash.</param>
/// <param name="Version">The author's version.</param>
/// <param name="Urgency">The recipe's urgency.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceRecipe(
    [property: JsonPropertyName("id")] string Name,
    string Hash,
    int Version,
    RecipeUrgency Urgency);
