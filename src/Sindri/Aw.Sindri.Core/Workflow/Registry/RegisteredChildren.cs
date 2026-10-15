using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>The child recipes a served validation resolves from the registered recipes (spec §9.1), read before validation so the validator stays pure.</summary>
/// <param name="byId">The recipe each child id resolves to.</param>
internal sealed class RegisteredChildren(IReadOnlyDictionary<RecipeId, ChildRecipe> byId) : IChildRecipeResolver
{
    /// <inheritdoc/>
    public ChildRecipe? Resolve(RecipeId id) => byId.GetValueOrDefault(id);
}
