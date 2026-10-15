using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The input of one 'RecipeWorkflow' execution (engine spec rev 8 §5.1): the recipe '(id, version, hash)', the item,
/// the attempt, the input facts, and the tenant and ring keys. The workflow id is <see cref="WorkflowId"/>, so starting
/// the same '(recipe, item, attempt)' again finds the running execution (WorkflowIdConflictPolicy.UseExisting).
/// </summary>
/// <param name="Recipe">The approved recipe the instance is pinned to.</param>
/// <param name="Item">The item, as IItems resolved it.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Inputs">The input facts by key ('fact:spec'), each stored by the API before the start.</param>
/// <param name="Tenant">The tenant key.</param>
/// <param name="Ring">The ring.</param>
public sealed record InstanceStart(
    RecipeVersion Recipe,
    ItemKey Item,
    int Attempt,
    IReadOnlyDictionary<FactKey, InputFact> Inputs,
    string Tenant,
    int Ring)
{
    /// <summary>The workflow id: the instance's address, '{item}/{recipe name}/{attempt}'.</summary>
    /// <returns>The address.</returns>
    public InstanceAddress WorkflowId() => InstanceAddress.Root(Item, Recipe.Id, Attempt);
}
