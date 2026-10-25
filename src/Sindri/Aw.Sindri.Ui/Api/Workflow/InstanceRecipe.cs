namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The recipe an instance runs.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Hash">Hash, as served.</param>
/// <param name="Version">The recipe author's version.</param>
/// <param name="Urgency">Urgency, as served.</param>
/// <param name="DisplayName">The workflow's display name (sentence case, at most 60 characters); null from a server older than the contract.</param>
internal sealed record InstanceRecipe(
    string Id,
    string Hash,
    int Version,
    RecipeUrgency Urgency,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null);
