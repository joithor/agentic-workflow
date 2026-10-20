using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe as an instance pins it (E-20).</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The pinned hash ("sha256:" and 64 hex digits).</param>
/// <param name="Version">The recipe author's version.</param>
/// <param name="Urgency">The recipe's urgency.</param>
/// <param name="DisplayName">The name shown instead of the id (setup-once spec §7.7); null, and left out of the JSON, when the engine knows none yet (the field is required on a recipe file once RCP-DISPLAY-001 lands). The app makes the id readable itself.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceRecipe(
    RecipeId Id,
    RecipeHash Hash,
    int Version,
    RecipeUrgency Urgency,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null);
