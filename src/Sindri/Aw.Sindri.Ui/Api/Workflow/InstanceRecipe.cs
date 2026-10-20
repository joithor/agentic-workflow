namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The recipe an instance runs.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Hash">Hash, as served.</param>
/// <param name="Version">The recipe author's version.</param>
/// <param name="Urgency">Urgency, as served.</param>
/// <param name="DisplayName">What the recipe does, in a sentence; read as optional (an older Sindri serves none).</param>
internal sealed record InstanceRecipe(
    string Id,
    string Hash,
    int Version,
    RecipeUrgency Urgency,
    string? DisplayName = null);
