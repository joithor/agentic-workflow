namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The recipe an instance runs.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Hash">Hash, as served.</param>
/// <param name="Version">Version, as served.</param>
/// <param name="Urgency">Urgency, as served.</param>
internal sealed record InstanceRecipe(
    string Id,
    string Hash,
    int Version,
    RecipeUrgency Urgency);
